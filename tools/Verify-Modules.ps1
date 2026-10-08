param([string]$GameDir = $env:GEKIDRIVE_GAME_DIR)
$ErrorActionPreference = 'Stop'
if (!$GameDir) { $GameDir = Join-Path $PSScriptRoot '..\GameFiles\package' }
Add-Type -Path (Join-Path $GameDir 'BepInEx\core\Mono.Cecil.dll')
$game = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $GameDir 'mu3_Data\Managed\Assembly-CSharp.dll'))
$audio = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $GameDir 'mu3_Data\Managed\Assembly-CSharp-firstpass.dll'))
$plugin = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $PSScriptRoot '..\artifacts\GekiDrive.Public.dll'))
try {
    $lookup = @{}
    $queue = [System.Collections.Queue]::new()
    foreach ($item in @($game.MainModule.Types) + @($audio.MainModule.Types)) { $queue.Enqueue($item) }
    while ($queue.Count -gt 0) { $item = $queue.Dequeue(); $lookup[$item.FullName] = $item; foreach ($nested in $item.NestedTypes) { $queue.Enqueue($nested) } }
    $checked = 0
    foreach ($type in $plugin.MainModule.Types) {
        $patches = @($type.CustomAttributes | Where-Object AttributeType -Match 'HarmonyPatch$')
        foreach ($attribute in $patches) {
            $typeArg = $attribute.ConstructorArguments | Where-Object { $_.Type.FullName -eq 'System.Type' }
            $nameArg = $attribute.ConstructorArguments | Where-Object { $_.Type.FullName -eq 'System.String' }
            if (!$typeArg -or !$nameArg) { continue }
            $target = $lookup[$typeArg.Value.FullName]
            if (!$target) { throw "Unknown patch target: $($type.FullName)" }
            $methods = @($target.Methods | Where-Object Name -eq $nameArg.Value)
            $argTypes = $attribute.ConstructorArguments | Where-Object { $_.Type.FullName -eq 'System.Type[]' }
            if ($argTypes) { $signature = ($argTypes.Value | ForEach-Object { $_.Value.FullName }) -join ','; $methods = @($methods | Where-Object { ($_.Parameters.ParameterType.FullName -join ',') -eq $signature }) }
            if ($methods.Count -ne 1) { throw "Ambiguous/missing target for $($type.FullName): $($nameArg.Value)" }
            $method = $methods[0]
            foreach ($patchMethod in $type.Methods | Where-Object { $_.CustomAttributes.AttributeType.FullName -match 'Harmony(Prefix|Postfix|Finalizer)$' }) {
                foreach ($parameter in $patchMethod.Parameters) {
                    $expected = $parameter.ParameterType.FullName -replace '&$', ''
                    if ($parameter.Name.StartsWith('___')) {
                        $fieldName = $parameter.Name.Substring(3)
                        $field = $target.Fields | Where-Object Name -eq $fieldName
                        if (!$field -or $field.FieldType.FullName -ne $expected) { throw "Field injection mismatch $($type.Name): $fieldName" }
                    } elseif ($parameter.Name -eq '__instance') {
                        if ($target.FullName -ne $expected) { throw "Instance injection mismatch: $($type.Name)" }
                    } elseif ($parameter.Name -match '^__(\d+)$') {
                        $index = [int]$Matches[1]
                        if ($index -ge $method.Parameters.Count -or $method.Parameters[$index].ParameterType.FullName -ne $expected) { throw "Indexed argument mismatch: $($type.Name)" }
                    } elseif (!$parameter.Name.StartsWith('__')) {
                        $native = $method.Parameters | Where-Object Name -eq $parameter.Name
                        if (!$native -or $native.ParameterType.FullName -ne $expected) { throw "Named argument mismatch $($type.Name): $($parameter.Name)" }
                    }
                }
            }
            $checked++
        }
    }
    foreach ($name in @('calcTimingNote','calcTimingBell','calcTimingBullet','calcTimingEvent')) {
        $skill = $game.MainModule.Types | Where-Object FullName -eq 'MU3.Skill.SkillManager'
        if (@($skill.Methods | Where-Object { $_.Name -eq $name -and $_.Parameters.Count -eq 0 }).Count -ne 1) { throw "Replay skill hook missing: $name" }
    }
    $notes = $game.MainModule.Types | Where-Object FullName -eq 'MU3.Notes.NotesManager'
    $seed = $notes.Properties | Where-Object Name -eq 'RandomShellSeedBase'
    if (!$seed.SetMethod -or $seed.SetMethod.Parameters[0].ParameterType.FullName -ne 'System.Int32') { throw 'Replay seed setter mismatch' }
    foreach ($pair in @(@('CriAtomExPlayer','SetDspTimeStretchRatio'),@('CriAtomExPlayer','SetVoicePoolIdentifier'),@('CriAtomExPlayer','UpdateAll'),@('CriAtomExVoicePool','AttachDspTimeStretch'))) {
        $native = $audio.MainModule.Types | Where-Object FullName -eq $pair[0]
        if (!($native.Methods | Where-Object Name -eq $pair[1])) { throw "Missing pitch-preserving DSP API: $($pair[1])" }
    }
    if (!($plugin.MainModule.Resources | Where-Object Name -eq 'GekiDrive.overlay.html')) { throw 'Embedded OBS overlay missing' }
    if ($plugin.MainModule.Types | Where-Object Name -match 'AutoZero') { throw 'Experimental zero-result code leaked into main' }
    Write-Host "PASS: $checked Harmony targets and injected fields/arguments, skill hooks, replay seed, CRI DSP APIs, embedded OBS UI and main/experiment separation. Runtime behavior is not verified."
} finally { $game.Dispose(); $audio.Dispose(); $plugin.Dispose() }
