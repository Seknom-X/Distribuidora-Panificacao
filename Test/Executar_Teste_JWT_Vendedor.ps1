param([string]$Api = 'http://localhost:5210', [string]$EmailVendedor = '', [int]$ClienteDeOutroVendedorId=0)
$ErrorActionPreference='Stop'
function CodigoHttp([scriptblock]$Comando){try{ & $Comando | Out-Null; return 200 }catch{if($null -eq $_.Exception.Response){throw}return [int]$_.Exception.Response.StatusCode}}
function Garantir($ok,$msg){if(-not $ok){throw "FALHA: $msg"}Write-Host "OK: $msg" -ForegroundColor Green}
if(-not $EmailVendedor){$EmailVendedor=Read-Host 'Email do usuario vendedor'}
$s=Read-Host 'Senha do vendedor (oculta)' -AsSecureString
$pwd=(New-Object System.Management.Automation.PSCredential('x',$s)).GetNetworkCredential().Password
$l=Invoke-RestMethod "$Api/api/auth/login" -Method Post -ContentType 'application/json' -Body (@{email=$EmailVendedor;senha=$pwd}|ConvertTo-Json)
$pwd=$null;$s=$null
Garantir ($l.usuario.perfil -eq 'Vendedor') 'Login retorna perfil Vendedor'
$id=[int]$l.usuario.vendedorId
$h=@{Authorization="Bearer $($l.token)"}
$me=Invoke-RestMethod "$Api/api/auth/me" -Headers $h
Garantir ([int]$me.vendedorId -eq $id) 'Token vinculado ao vendedor correto'
$carteira=@(Invoke-RestMethod "$Api/api/me/clientes" -Headers $h)
$pedidos=@(Invoke-RestMethod "$Api/api/me/pedidos" -Headers $h)
$null=Invoke-RestMethod "$Api/api/me/produtos" -Headers $h
Write-Host "Carteira consultada. Clientes encontrados: $($carteira.Count). Pedidos encontrados: $($pedidos.Count)."
$c=CodigoHttp { Invoke-RestMethod "$Api/api/produtos" -Headers $h -ErrorAction Stop }
Garantir ($c -eq 403) 'Vendedor NAO acessa CRUD administrativo de produtos -> 403'
$c=CodigoHttp { Invoke-RestMethod "$Api/api/vendedores" -Headers $h -ErrorAction Stop }
Garantir ($c -eq 403) 'Vendedor NAO lista todos os vendedores -> 403'
$c=CodigoHttp { Invoke-RestMethod "$Api/api/auth/usuarios" -Headers $h -ErrorAction Stop }
Garantir ($c -eq 403) 'Vendedor NAO lista usuarios -> 403'
if($ClienteDeOutroVendedorId -gt 0){
 $c=CodigoHttp { Invoke-RestMethod "$Api/api/me/clientes/$ClienteDeOutroVendedorId" -Headers $h -ErrorAction Stop }
 Garantir ($c -eq 404) 'Cliente de OUTRO vendedor nao aparece -> 404'
}
Write-Host 'TESTES JWT VENDEDOR APROVADOS: nenhuma baixa no MySQL.' -ForegroundColor Green
