using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Distribuidora.API.Data;
using Distribuidora.API.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace Distribuidora.API.Security;

public static class SegurancaDistribuidora
{
    public const string VendedorOuAdmin = "VendedorOuAdmin";

    public static IServiceCollection AddSegurancaDistribuidora(
        this IServiceCollection services, IConfiguration configuracao)
    {
        var chaveBase64 = configuracao["Jwt:Chave"];
        var emissor = configuracao["Jwt:Emissor"];
        var audiencia = configuracao["Jwt:Audiencia"];

        byte[] chave;
        try { chave = Convert.FromBase64String(chaveBase64 ?? ""); }
        catch (FormatException) { throw new InvalidOperationException("Jwt:Chave deve ser Base64."); }

        if (chave.Length < 32 || string.IsNullOrWhiteSpace(emissor) ||
            string.IsNullOrWhiteSpace(audiencia))
            throw new InvalidOperationException(
                "Configure Jwt:Chave (Base64, minimo 32 bytes), Jwt:Emissor e Jwt:Audiencia nos User Secrets.");

        services.AddScoped<IPasswordHasher<UsuarioSistema>, PasswordHasher<UsuarioSistema>>();
        services.AddSingleton(new DadosJwt(chave, emissor, audiencia));
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(opcoes =>
            {
                opcoes.MapInboundClaims = false;
                opcoes.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = emissor,
                    ValidateAudience = true,
                    ValidAudience = audiencia,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(chave),
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    RequireSignedTokens = true,
                    ClockSkew = TimeSpan.FromSeconds(15),
                    RoleClaimType = ClaimTypes.Role,
                    NameClaimType = ClaimTypes.NameIdentifier
                };
                opcoes.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async contexto =>
                    {
                        var idTexto = contexto.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                        var papel = contexto.Principal?.FindFirstValue(ClaimTypes.Role);
                        var claimVendedor = contexto.Principal?.FindFirstValue("vendedorId");
                        if (!int.TryParse(idTexto, out var id))
                        {
                            contexto.Fail("Identificador de usuario invalido.");
                            return;
                        }
                        var banco = contexto.HttpContext.RequestServices
                            .GetRequiredService<DistribuidoraDbContext>();
                        var usuario = await banco.Usuarios.AsNoTracking()
                            .Include(u => u.Vendedor)
                            .FirstOrDefaultAsync(u => u.Id == id);
                        if (usuario is null || !usuario.Ativo || usuario.Perfil != papel)
                        {
                            contexto.Fail("Usuario inativo ou perfil alterado.");
                            return;
                        }
                        if (usuario.Perfil == PerfisSistema.Vendedor &&
                            (usuario.VendedorId is null || usuario.Vendedor is not { Ativo: true } ||
                             claimVendedor != usuario.VendedorId.Value.ToString()))
                        {
                            contexto.Fail("Vendedor desativado ou vinculo alterado.");
                            return;
                        }
                        if (usuario.Perfil == PerfisSistema.Admin && claimVendedor is not null)
                            contexto.Fail("Credenciais inconsistentes.");
                    }
                };
            });

        // Regra de seguranca: controllers antigos so podem ser usados por Admin.
        // Apenas as rotas explicitamente anotadas para Vendedor permitem esse perfil.
        services.AddAuthorization(opcoes =>
        {
            var somenteAdmin = new AuthorizationPolicyBuilder(JwtBearerDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser()
                .RequireRole(PerfisSistema.Admin)
                .Build();
            opcoes.DefaultPolicy = somenteAdmin;
            opcoes.FallbackPolicy = somenteAdmin;
            opcoes.AddPolicy(VendedorOuAdmin, politica => politica
                .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser()
                .RequireRole(PerfisSistema.Admin, PerfisSistema.Vendedor));
        });
        return services;
    }

    // Executar exclusivamente no terminal local em Development: --criar-admin.
    // Nunca disponibilizar a criacao inicial de Admin por HTTP.
    public static async Task CriarAdminLocalAsync(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
            throw new InvalidOperationException("Criacao de Admin disponivel somente em Development.");

        await using var escopo = app.Services.CreateAsyncScope();
        var banco = escopo.ServiceProvider.GetRequiredService<DistribuidoraDbContext>();
        if (!await banco.Database.CanConnectAsync())
            throw new InvalidOperationException("MySQL indisponivel. Confira o Docker e os User Secrets.");
        if (await banco.Usuarios.AnyAsync())
        {
            Console.WriteLine("Ja existem usuarios. Nao e possivel recriar o Admin pelo bootstrap.");
            return;
        }

        Console.Write("Nome do Admin: ");
        var nome = (Console.ReadLine() ?? "").Trim();
        Console.Write("E-mail do Admin: ");
        var email = (Console.ReadLine() ?? "").Trim().ToLowerInvariant();
        Console.Write("Senha (oculta, minimo 12 caracteres): ");
        var senha = LerSenhaOculta();
        Console.Write("Repita a senha: ");
        var repeticao = LerSenhaOculta();

        if (nome.Length is < 2 or > 150 || email.Length > 180 ||
            !System.Net.Mail.MailAddress.TryCreate(email, out var endereco) ||
            !string.Equals(endereco.Address, email, StringComparison.OrdinalIgnoreCase) ||
            senha.Length is < 12 or > 128 || senha != repeticao)
        {
            Console.WriteLine("Dados invalidos ou senhas divergentes. Admin nao criado.");
            return;
        }

        var usuario = new UsuarioSistema
        {
            Nome = nome,
            Email = email,
            Perfil = PerfisSistema.Admin,
            Ativo = true
        };
        var hasher = escopo.ServiceProvider.GetRequiredService<IPasswordHasher<UsuarioSistema>>();
        usuario.SenhaHash = hasher.HashPassword(usuario, senha);
        banco.Usuarios.Add(usuario);
        await banco.SaveChangesAsync();
        Console.WriteLine($"Administrador criado. ID={usuario.Id}. Senha nunca armazenada em texto puro.");
    }

    private static string LerSenhaOculta()
    {
        var caracteres = new List<char>();
        while (true)
        {
            var tecla = Console.ReadKey(intercept: true);
            if (tecla.Key == ConsoleKey.Enter) { Console.WriteLine(); break; }
            if (tecla.Key == ConsoleKey.Backspace)
            {
                if (caracteres.Count > 0) caracteres.RemoveAt(caracteres.Count - 1);
                continue;
            }
            if (!char.IsControl(tecla.KeyChar)) caracteres.Add(tecla.KeyChar);
        }
        return new string(caracteres.ToArray());
    }
}

public sealed record DadosJwt(byte[] Chave, string Emissor, string Audiencia);

