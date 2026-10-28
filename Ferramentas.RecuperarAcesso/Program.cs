using Distribuidora.API.Data;
using Distribuidora.API.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MySqlConnector;

if (!string.Equals(
        Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
        "Development", StringComparison.OrdinalIgnoreCase))
{
    Console.WriteLine("ERRO: ferramenta permitida somente em Development.");
    return;
}

var configuracao = new ConfigurationBuilder()
    .AddUserSecrets(typeof(DistribuidoraDbContext).Assembly, optional: true)
    .Build();

var conexao = configuracao.GetConnectionString("DistribuidoraDb");
if (string.IsNullOrWhiteSpace(conexao))
{
    Console.WriteLine("ERRO: ConnectionStrings:DistribuidoraDb ausente nos User Secrets da API.");
    return;
}

MySqlConnectionStringBuilder info;
try
{
    info = new MySqlConnectionStringBuilder(conexao);
}
catch
{
    Console.WriteLine("ERRO: a connection string do MySQL tem formato invalido.");
    return;
}

if ((info.Server != "localhost" && info.Server != "127.0.0.1") ||
    info.Port != 3307 ||
    !string.Equals(info.Database, "DistribuidoraDb", StringComparison.OrdinalIgnoreCase))
{
    Console.WriteLine("ERRO: por seguranca, esta ferramenta aceita somente");
    Console.WriteLine("MySQL local (localhost:3307), banco DistribuidoraDb.");
    return;
}

try
{
    var opcoes = new DbContextOptionsBuilder<DistribuidoraDbContext>()
        .UseMySql(conexao, ServerVersion.AutoDetect(conexao))
        .Options;

    await using var banco = new DistribuidoraDbContext(opcoes);

    var admins = await banco.Usuarios.AsNoTracking()
        .Where(u => u.Perfil == PerfisSistema.Admin)
        .OrderBy(u => u.Id)
        .Select(u => new { u.Id, u.Nome, u.Email, u.Ativo })
        .ToListAsync();

    if (admins.Count == 0)
    {
        var existeOutroUsuario = await banco.Usuarios.AnyAsync();
        Console.WriteLine("Nenhuma conta de Administrador foi encontrada.");
        if (!existeOutroUsuario)
        {
            Console.WriteLine("O banco nao possui usuarios. Crie o primeiro Admin:");
            Console.WriteLine("dotnet run --project .\\Distribuidora.API -- --criar-admin");
        }
        else
        {
            Console.WriteLine("Existem usuarios, mas nenhum Admin.");
            Console.WriteLine("Nao exclua os usuarios; revise o cadastro antes de continuar.");
        }
        return;
    }

    Console.WriteLine("Administradores encontrados no banco local:");
    foreach (var adm in admins)
        Console.WriteLine($"ID={adm.Id} | Nome={adm.Nome} | Email={adm.Email} | Ativo={adm.Ativo}");

    Console.Write("Digite o ID do Admin cuja senha sera redefinida (0 cancela): ");
    if (!int.TryParse(Console.ReadLine(), out var id) || id == 0)
    {
        Console.WriteLine("Operacao cancelada.");
        return;
    }

    var escolhido = admins.FirstOrDefault(a => a.Id == id);
    if (escolhido is null)
    {
        Console.WriteLine("ID nao pertence a um Administrador.");
        return;
    }
    if (!escolhido.Ativo)
    {
        Console.WriteLine("A conta esta inativa; redefinir a senha nao a reativa.");
        return;
    }

    Console.Write($"Digite CONFIRMAR para redefinir a senha de {escolhido.Email}: ");
    if (Console.ReadLine() != "CONFIRMAR")
    {
        Console.WriteLine("Operacao cancelada.");
        return;
    }

    Console.Write("Nova senha (oculta, 12 a 128 caracteres): ");
    var novaSenha = LerSenhaOculta();

    Console.Write("Repita a senha (oculta): ");
    var confirmacao = LerSenhaOculta();

    if (novaSenha.Length < 12 || novaSenha.Length > 128 || novaSenha != confirmacao)
    {
        Console.WriteLine("Senha invalida ou confirmacao diferente. Nada foi alterado.");
        return;
    }

    var usuario = await banco.Usuarios
        .SingleOrDefaultAsync(u => u.Id == id && u.Perfil == PerfisSistema.Admin && u.Ativo);
    if (usuario is null)
    {
        Console.WriteLine("Administrador nao encontrado ou inativo. Nada foi alterado.");
        return;
    }

    var hash = new PasswordHasher<UsuarioSistema>().HashPassword(usuario, novaSenha);
    novaSenha = string.Empty;
    confirmacao = string.Empty;

    usuario.SenhaHash = hash;
    await banco.SaveChangesAsync();

    Console.WriteLine("OK: senha do Administrador redefinida com PasswordHasher.");
    Console.WriteLine($"Email de login: {usuario.Email}");
    Console.WriteLine("Nenhum outro cadastro, pedido ou estoque foi alterado.");
}
catch (Exception ex)
{
    Console.WriteLine($"ERRO ao acessar o banco: {ex.GetType().Name}.");
    Console.WriteLine("Confirme que o Docker esta ligado, que a migration UsuariosAutenticacao");
    Console.WriteLine("foi aplicada e que a connection string esta nos User Secrets da API.");
}

static string LerSenhaOculta()
{
    var caracteres = new List<char>();
    while (true)
    {
        var tecla = Console.ReadKey(intercept: true);
        if (tecla.Key == ConsoleKey.Enter)
        {
            Console.WriteLine();
            break;
        }
        if (tecla.Key == ConsoleKey.Backspace)
        {
            if (caracteres.Count > 0) caracteres.RemoveAt(caracteres.Count - 1);
            continue;
        }
        if (!char.IsControl(tecla.KeyChar))
            caracteres.Add(tecla.KeyChar);
    }
    return new string(caracteres.ToArray());
}
