# Incrementa a versão do projeto.
#
# O esquema é x.y: o segundo número sobe a cada alteração entregue. Manter isso à mão
# leva a esquecer, e aí a tela mostra uma versão que não corresponde ao binário — o
# problema que a versão existe para evitar.
#
#   .\scripts\versao.ps1                incrementa (1.2 -> 1.3)
#   .\scripts\versao.ps1 -Ver           só mostra a versão atual
#   .\scripts\versao.ps1 -Definir 2.0   fixa um valor
#
# Sobre codificação: este script usa [System.IO.File] em vez de Get-Content e
# Set-Content. No PowerShell 5.1, Get-Content lê como ANSI e Set-Content -Encoding utf8
# regrava, o que codifica os acentos duas vezes: "lê" vira "lÃª" em todos os arquivos
# tocados. Ler e escrever UTF-8 explicitamente evita isso.

param(
    [switch]$Ver,
    [string]$Definir
)

$raiz = Split-Path $PSScriptRoot -Parent
$csproj = Join-Path $raiz "src\CaixaInterativa\CaixaInterativa.csproj"
if (-not (Test-Path $csproj)) { Write-Error "csproj não encontrado em $csproj"; exit 1 }

# UTF-8 sem BOM: adicionar BOM ao csproj quebraria o MSBuild em alguns casos.
$utf8 = New-Object System.Text.UTF8Encoding($false)

function LerTexto([string]$caminho) {
    return [System.IO.File]::ReadAllText($caminho, [System.Text.Encoding]::UTF8)
}
function EscreverTexto([string]$caminho, [string]$texto) {
    [System.IO.File]::WriteAllText($caminho, $texto, $utf8)
}

$conteudo = LerTexto $csproj
if ($conteudo -notmatch '<Version>([\d\.]+)</Version>') {
    Write-Error "Não achei a tag <Version> no csproj"; exit 1
}
$atual = $matches[1]

if ($Ver) { Write-Output $atual; exit 0 }

if ($Definir) {
    $nova = $Definir
} else {
    # Aceita x.y e x.y.z, e sempre devolve x.y — o esquema do projeto.
    $partes = $atual.Split('.')
    $nova = "$([int]$partes[0]).$([int]$partes[1] + 1)"
}

EscreverTexto $csproj ($conteudo -replace '<Version>[\d\.]+</Version>', "<Version>$nova</Version>")
Write-Host "$atual -> $nova"

# O README carrega a versão em três lugares; deixá-los para trás faz a documentação
# mentir sobre o que o usuário baixou.
$readme = Join-Path $raiz "README.md"
if (Test-Path $readme) {
    $r = LerTexto $readme
    $r = $r -replace 'CaixaInterativa-v[\d\.]+-win-x64\.exe', "CaixaInterativa-v$nova-win-x64.exe"
    $r = $r -replace '`v[\d\.]+`', "``v$nova``"
    $r = $r -replace '(\| \*\*Versão atual\*\* \| )[\d\.]+( \|)', "`${1}$nova`${2}"
    $r = $r -replace '(Versão )[\d\.]+(\. Projeto Caixa de Areia)', "`${1}$nova`${2}"
    EscreverTexto $readme $r
    Write-Host "README atualizado"
}

Write-Output $nova
