using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Distribuidora.API.Data;
using Distribuidora.API.DTOs;
using Distribuidora.API.Models;
using Distribuidora.API.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace Distribuidora.API.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AutenticacaoController : ControllerBase
{
    private readonly DistribuidoraDbContext _banco;
    private readonly IPasswordHasher<UsuarioSistema> _hasher;
    private readonly DadosJwt _jwt;

    public AutenticacaoController(DistribuidoraDbContext banco,
        IPasswordHasher<UsuarioSistema> hasher, DadosJwt jwt)
    {
        _banco = banco;
        _hasher = hasher;
        _jwt = jwt;
    }

    [AllowAnonymous]
    [HttpPost("login")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto dados, CancellationToken ct)
    {
        var email = dados.Email.Trim().ToLowerInvariant();
        var usuario = await _banco.Usuarios
            .Include(u => u.Vendedor)
            .FirstOrDefaultAsync(u => u.Email == email, ct);

        if (usuario is null || !usuario.Ativo ||
            (usuario.Perfil == PerfisSistema.Vendedor && usuario.Vendedor is not { Ativo: true }))
            return Unauthorized(new { mensagem = "Credenciais invalidas." });

        var verificacao = _hasher.VerifyHashedPassword(usuario, usuario.SenhaHash, dados.Senha);
        if (verificacao == PasswordVerificationResult.Failed)
            return Unauthorized(new { mensagem = "Credenciais invalidas." });

        if (verificacao == PasswordVerificationResult.SuccessRehashNeeded)
        {
            usuario.SenhaHash = _hasher.HashPassword(usuario, dados.Senha);
            await _banco.SaveChangesAsync(ct);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
            new(ClaimTypes.Name, usuario.Nome),
            new(ClaimTypes.Role, usuario.Perfil),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("D"))
        };
        if (usuario.VendedorId is int vendedorId)
            claims.Add(new Claim("vendedorId", vendedorId.ToString()));

        var validade = DateTime.UtcNow.AddMinutes(60);
        var token = new JwtSecurityToken(
            issuer: _jwt.Emissor, audience: _jwt.Audiencia,
            claims: claims, notBefore: DateTime.UtcNow, expires: validade,
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(_jwt.Chave), SecurityAlgorithms.HmacSha256));

        return Ok(new
        {
            token = new JwtSecurityTokenHandler().WriteToken(token),
            tipo = "Bearer",
            expiraEmUtc = validade,
            usuario = new { usuario.Id, usuario.Nome, usuario.Email, usuario.Perfil, usuario.VendedorId }
        });
    }

    [Authorize(Policy = SegurancaDistribuidora.VendedorOuAdmin)]
    [HttpGet("me")]
    public IActionResult MeuUsuario() => Ok(new
    {
        id = User.FindFirstValue(ClaimTypes.NameIdentifier),
        nome = User.FindFirstValue(ClaimTypes.Name),
        perfil = User.FindFirstValue(ClaimTypes.Role),
        vendedorId = User.FindFirstValue("vendedorId")
    });

    [Authorize(Roles = PerfisSistema.Admin)]
    [HttpGet("usuarios")]
    public async Task<IActionResult> ListarUsuarios(CancellationToken ct)
    {
        var usuarios = await _banco.Usuarios.AsNoTracking()
            .OrderBy(u => u.Id)
            .Select(u => new { u.Id, u.Nome, u.Email, u.Perfil, u.VendedorId, u.Ativo })
            .ToListAsync(ct);
        return Ok(usuarios);
    }

    [Authorize(Roles = PerfisSistema.Admin)]
    [HttpPost("usuarios/vendedor")]
    public async Task<IActionResult> CadastrarVendedor(
        [FromBody] CriarUsuarioVendedorDto dados, CancellationToken ct)
    {
        var email = dados.Email.Trim().ToLowerInvariant();
        if (!System.Net.Mail.MailAddress.TryCreate(email, out var endereco) ||
            !string.Equals(endereco.Address, email, StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { mensagem = "E-mail invalido." });

        var vendedor = await _banco.Vendedores.AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == dados.VendedorId && v.Ativo, ct);
        if (vendedor is null)
            return BadRequest(new { mensagem = "Vendedor nao encontrado ou inativo." });

        if (await _banco.Usuarios.AnyAsync(u => u.Email == email || u.VendedorId == dados.VendedorId, ct))
            return Conflict(new { mensagem = "E-mail ou vendedor ja possui conta de acesso." });

        var usuario = new UsuarioSistema
        {
            Nome = dados.Nome.Trim(), Email = email,
            Perfil = PerfisSistema.Vendedor,
            VendedorId = dados.VendedorId, Ativo = true
        };
        usuario.SenhaHash = _hasher.HashPassword(usuario, dados.Senha);
        _banco.Usuarios.Add(usuario);
        try
        {
            await _banco.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(new { mensagem = "E-mail ou vendedor ja cadastrado." });
        }
        return StatusCode(201, new { usuario.Id, usuario.Nome, usuario.Email,
            usuario.Perfil, usuario.VendedorId });
    }
}

