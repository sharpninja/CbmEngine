<#
  scaffold-cbmengine.ps1
  Run from the repo root:   cd f:\github\CbmEngine ;  .\scaffold-cbmengine.ps1
  Adds vice-sharp as a submodule and creates the CbmEngine solution + projects + references.
  Idempotent: safe to re-run; skips steps already done.
#>
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

# 0. Ensure this folder is a git repo (required for submodules).
if (-not (Test-Path .git)) { git init | Out-Host }

# 1. Add vice-sharp as a submodule under external/  (+ shield it from our Directory.Build.props).
New-Item -ItemType Directory -Force external | Out-Null
'<Project></Project>' | Set-Content -Encoding UTF8 external\Directory.Build.props
if (-not (Test-Path external\vice-sharp\src)) {
    git submodule add https://github.com/sharpninja/vice-sharp.git external/vice-sharp
    git submodule update --init --recursive
}

# 2. Solution.
if (-not (Test-Path CbmEngine.sln)) { dotnet new sln -n CbmEngine }

# 3. Shared build settings (root). The external/ shield above keeps these off the submodule.
@'
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <LangVersion>latest</LangVersion>
  </PropertyGroup>
</Project>
'@ | Set-Content -Encoding UTF8 Directory.Build.props

# 4. CbmEngine projects.
$projects = @(
  @{ n='CbmEngine.Abstractions';  t='classlib' },
  @{ n='CbmEngine.Systems';       t='classlib' },
  @{ n='CbmEngine.Pipeline';      t='classlib' },
  @{ n='CbmEngine.Host.MonoGame'; t='classlib' },
  @{ n='CbmEngine.Game.Sample';   t='console'  }
)
foreach ($p in $projects) {
  $dir = "src\$($p.n)"
  if (-not (Test-Path "$dir\$($p.n).csproj")) {
    dotnet new $($p.t) -n $($p.n) -o $dir
    Remove-Item "$dir\Class1.cs" -ErrorAction SilentlyContinue
  }
  dotnet sln CbmEngine.sln add "$dir\$($p.n).csproj"
}

# 5. Reference the ViceSharp projects (from the submodule).
$vs = 'external\vice-sharp\src'
function VS($name) { "$vs\$name\$name.csproj" }

dotnet add src\CbmEngine.Abstractions\CbmEngine.Abstractions.csproj reference (VS 'ViceSharp.Abstractions')

dotnet add src\CbmEngine.Systems\CbmEngine.Systems.csproj reference `
  (VS 'ViceSharp.Abstractions') (VS 'ViceSharp.Core') (VS 'ViceSharp.Chips') (VS 'ViceSharp.Architectures') `
  src\CbmEngine.Abstractions\CbmEngine.Abstractions.csproj

dotnet add src\CbmEngine.Host.MonoGame\CbmEngine.Host.MonoGame.csproj reference `
  src\CbmEngine.Systems\CbmEngine.Systems.csproj src\CbmEngine.Abstractions\CbmEngine.Abstractions.csproj

dotnet add src\CbmEngine.Game.Sample\CbmEngine.Game.Sample.csproj reference `
  src\CbmEngine.Host.MonoGame\CbmEngine.Host.MonoGame.csproj `
  src\CbmEngine.Systems\CbmEngine.Systems.csproj `
  src\CbmEngine.Abstractions\CbmEngine.Abstractions.csproj

# 6. MonoGame packages (host + content pipeline). MonoGame ships net8.0 assemblies, which a
#    net10.0 project consumes fine. If restore complains, pin a version, e.g. add  --version 3.8.*
dotnet add src\CbmEngine.Host.MonoGame\CbmEngine.Host.MonoGame.csproj package MonoGame.Framework.DesktopGL
dotnet add src\CbmEngine.Pipeline\CbmEngine.Pipeline.csproj package MonoGame.Framework.Content.Pipeline

# 7. .gitignore (skip if you already have one).
if (-not (Test-Path .gitignore)) {
@'
bin/
obj/
*.user
.vs/
'@ | Set-Content -Encoding UTF8 .gitignore
}

# 8. Restore now to surface any TFM/package issues immediately.
dotnet restore CbmEngine.sln
Write-Host "`n=== Scaffold complete. Drop C64CharsetProcessor.cs + BandedScreenAllocator.cs into src\CbmEngine.Pipeline\ ===" -ForegroundColor Green

<#
NOTES
- Project graph:
    Abstractions  -> ViceSharp.Abstractions
    Systems       -> ViceSharp.{Abstractions,Core,Chips,Architectures} + CbmEngine.Abstractions
    Pipeline      -> MonoGame.Framework.Content.Pipeline   (no ViceSharp ref; VicPalette is inlined)
    Host.MonoGame -> Systems + Abstractions + MonoGame.Framework.DesktopGL
    Game.Sample   -> Host + Systems + Abstractions
- TFM: net10.0 everywhere (matches ViceSharp). Host references Systems (net10), so Host must stay
  >= net10 — that's why we rely on MonoGame's net8.0 assemblies being consumable by net10, rather
  than downgrading Host.
- Drop the two pipeline source files you already have into src\CbmEngine.Pipeline\ after running.
- ViceSharp.SourceGen is consumed internally by the ViceSharp projects; CbmEngine does not reference it.
#>
