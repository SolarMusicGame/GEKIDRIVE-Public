param([string]$GameDir = $env:GEKIDRIVE_GAME_DIR)
$ErrorActionPreference = 'Stop'
if (!$GameDir) { $GameDir = Join-Path $PSScriptRoot '..\GameFiles\package' }
Add-Type -Path (Join-Path $GameDir 'BepInEx\core\Mono.Cecil.dll')
$game = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $GameDir 'mu3_Data\Managed\Assembly-CSharp.dll'))
$plugin = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $PSScriptRoot '..\artifacts\GekiDrive.Public.dll'))
try {
    $guard = $plugin.MainModule.Types | Where-Object Name -eq 'PublicScoreGuard'
    if (!$guard) { throw 'Public protection missing' }
    $registration = $plugin.MainModule.Types | Where-Object Name -eq 'Plugin'
    $attribute = $registration.CustomAttributes | Where-Object AttributeType -Match 'BepInPlugin$'
    if ($attribute.ConstructorArguments[0].Value -ne 'org.gekidrive.ongeki.public') { throw 'Wrong public plugin ID' }
    $install = $guard.Methods | Where-Object Name -eq 'Install'
    $names = @($install.Body.Instructions | Where-Object { $_.OpCode.Code -eq 'Ldstr' } | ForEach-Object Operand)
    $routes = @(
        @('MU3.Battle.GameEngine','applyResultToUserData','BlockResult'),
        @('MU3.User.UserLocal','addPlayLog','BlockPlayLog'),
        @('MU3.Client.PacketUpsertUserAll','create','BlockUploadCreate'),
        @('MU3.Client.PacketUpsertUserAll','proc','BlockUploadProcess'),
        @('MU3.Client.Packet','create','BlockGenericCreate'),
        @('MU3.Client.Packet','proc','BlockGenericProcess')
    )
    foreach ($route in $routes) {
        if ($names -notcontains $route[2]) { throw "Protection not installed: $($route[2])" }
        $target = $game.MainModule.Types | Where-Object FullName -eq $route[0]
        $native = @($target.Methods | Where-Object Name -eq $route[1])
        if ($native.Count -ne 1) { throw "Ambiguous guard target: $($route[0])::$($route[1])" }
        $prefix = $guard.Methods | Where-Object Name -eq $route[2]
        foreach ($parameter in $prefix.Parameters) {
            $type = $parameter.ParameterType.FullName -replace '&$', ''
            if ($parameter.Name -eq '__result' -and $type -ne $native[0].ReturnType.FullName) { throw "Result type mismatch: $($route[2])" }
            if ($parameter.Name -eq '__instance' -and $type -ne $target.FullName) { throw "Instance mismatch: $($route[2])" }
            if ($parameter.Name -eq '__0' -and $type -ne $native[0].Parameters[0].ParameterType.FullName) { throw "Query mismatch: $($route[2])" }
        }
    }
    foreach ($name in @('BlockResult','BlockPlayLog','BlockUploadCreate','BlockUploadProcess')) {
        $method = $guard.Methods | Where-Object Name -eq $name
        $instructions = @($method.Body.Instructions)
        for ($i = 0; $i -lt $instructions.Count; $i++) {
            if ($instructions[$i].OpCode.Code -eq 'Ret' -and $instructions[$i - 1].OpCode.Code -ne 'Ldc_I4_0') { throw "Guard can allow native execution: $name" }
            if ($instructions[$i].Operand -and $instructions[$i].Operand.ToString() -match 'ModuleConfig|Training::|ConfigEntry') { throw "Config can affect guard: $name" }
        }
    }
    $packet = $game.MainModule.Types | Where-Object FullName -eq 'MU3.Client.Packet'
    foreach ($field in @('state_','status_')) { if (!($packet.Fields | Where-Object Name -eq $field)) { throw "Packet completion field missing: $field" } }
    foreach ($name in @('BlockGenericCreate','BlockGenericProcess')) {
        $method = $guard.Methods | Where-Object Name -eq $name
        $types = @($method.Body.Instructions | Where-Object { $_.OpCode.Code -eq 'Isinst' } | ForEach-Object { $_.Operand.FullName })
        if ($types -notcontains 'MU3.Client.UpsertUserAll' -or $types -notcontains 'MU3.Client.PacketUpsertUserAll') { throw "Generic upload filter incomplete: $name" }
    }
    $calls = @($registration.Methods | Where-Object Name -eq 'Awake' | ForEach-Object { $_.Body.Instructions | Where-Object { $_.Operand -and $_.Operand.ToString() -match 'PublicScoreGuard::Install' } })
    if ($calls.Count -ne 1) { throw 'Mandatory guard installation missing' }
    Write-Host 'PASS: separate public identity, six mandatory score/save/upload guards, unconditional skips, query filters, packet completion fields and startup installation. Game/server runtime remains unverified.'
} finally { $game.Dispose(); $plugin.Dispose() }
