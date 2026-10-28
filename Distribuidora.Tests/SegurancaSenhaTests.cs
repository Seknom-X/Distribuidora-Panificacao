using System.ComponentModel.DataAnnotations;
using Distribuidora.API.DTOs;
namespace Distribuidora.Tests;
public class SegurancaSenhaTests {
 [Theory][InlineData("curta",false)][InlineData("abcdefghijkl",true)]
 public void UsuarioVendedorExigeDozeCaracteres(string senha,bool esperado) {
  var dto=new CriarUsuarioVendedorDto{Nome="Vendedor",Email="teste@example.com",Senha=senha,VendedorId=1};
  Assert.Equal(esperado,Validator.TryValidateObject(dto,new ValidationContext(dto),new List<ValidationResult>(),true));
 }
}
