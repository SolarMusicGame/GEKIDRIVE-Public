param([string]$GameDir = $env:GEKIDRIVE_GAME_DIR)
$ErrorActionPreference = 'Stop'
if (!$GameDir) { $GameDir = Join-Path $PSScriptRoot '..\GameFiles\package' }
Add-Type -Path (Join-Path $GameDir 'BepInEx\core\Mono.Cecil.dll')
$gamePath = Join-Path $GameDir 'mu3_Data\Managed\Assembly-CSharp.dll'
$game = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($gamePath)
$plugin = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $PSScriptRoot '..\artifacts\GekiDrive.Public.dll'))
try {
    $hash = (Get-FileHash -LiteralPath $gamePath -Algorithm SHA256).Hash.ToLowerInvariant()
    $expected = '0766056f0bb6e273417be31aa9f266d71589cd5d78abd5f022173d47f04f0a4a'
    if ($hash -ne $expected) { throw 'Game fingerprint mismatch' }
    if ($plugin.MainModule.RuntimeVersion -ne 'v2.0.50727') { throw 'Plugin must target CLR 2.0' }
    $scene = $game.MainModule.Types | Where-Object FullName -eq 'MU3.Scene_32_PrePlayMusic_MusicSelect'
    foreach ($name in @('Enter_Select', 'Leave_Select', 'OnDestroy')) {
        $methods = @($scene.Methods | Where-Object { $_.Name -eq $name -and $_.Parameters.Count -eq 0 -and $_.ReturnType.FullName -eq 'System.Void' })
        if ($methods.Count -ne 1) { throw "Scene hook mismatch: $name" }
    }
    $ui = $game.MainModule.Types | Where-Object FullName -eq 'MU3.SystemUI'
    $timer = $ui.NestedTypes | Where-Object Name -eq 'Timer'
    $execute = @($timer.Methods | Where-Object { $_.Name -eq 'execute' -and $_.Parameters.Count -eq 0 })
    if ($execute.Count -ne 1) { throw 'Timer.execute signature mismatch' }
    $calls = @($execute[0].Body.Instructions | Where-Object { $_.OpCode.Name -eq 'call' -and $_.Operand.ToString() -eq 'System.Single UnityEngine.Time::get_deltaTime()' })
    if ($calls.Count -ne 1) { throw 'Expected exactly one deltaTime call' }
    $notes = $game.MainModule.Types | Where-Object FullName -eq 'MU3.Notes.NotesManager'
    $damage = $game.MainModule.Types | Where-Object FullName -eq 'MU3.Notes.Damage'
    if (!($damage.Fields | Where-Object { $_.Name -eq 'Beam' -and $_.Constant -eq 9 })) { throw 'Beam damage enum mismatch' }
    $damagePlayer = @($notes.Methods | Where-Object { $_.Name -eq 'damagePlayer' -and ($_.Parameters.ParameterType.FullName -join ',') -eq 'MU3.Notes.Damage,System.Int32' })
    if ($damagePlayer.Count -ne 1 -or $damagePlayer[0].Parameters[0].Name -ne 'type') { throw 'Beam damage injection signature mismatch' }
    $beam = $game.MainModule.Types | Where-Object FullName -eq 'MU3.Notes.BeamNoteCore'
    $beamPlay = $beam.Methods | Where-Object Name -eq 'checkPlay'
    $beamCalls = @($beamPlay.Body.Instructions | Where-Object { $_.Operand -and $_.Operand.ToString() -eq 'System.Void MU3.Notes.NotesManager::damagePlayer(MU3.Notes.Damage,System.Int32)' })
    if ($beamCalls.Count -ne 1) { throw 'Beam no longer uses the intercepted damage path' }
    $beamTypeLoad = $beamCalls[0].Previous.Previous.Previous.Previous
    if ($beamTypeLoad.OpCode.Name -ne 'ldc.i4.s' -or $beamTypeLoad.Operand -ne 9) { throw 'Beam collision damage type changed' }
    $autoMethod = @($notes.Methods | Where-Object { $_.Name -eq 'isAutoPlay' -and $_.Parameters.Count -eq 0 -and $_.ReturnType.FullName -eq 'System.Boolean' -and !$_.IsStatic })
    if ($autoMethod.Count -ne 1) { throw 'AutoPlay hook signature mismatch' }
    foreach ($name in @('TapNoteCore', 'HoldNoteCore', 'FlickNoteCore')) {
        $type = $game.MainModule.Types | Where-Object FullName -eq ('MU3.Notes.' + $name)
        $autoCalls = @($type.Methods | Where-Object HasBody | ForEach-Object { $_.Body.Instructions | Where-Object { $_.Operand -and $_.Operand.ToString() -eq 'System.Boolean MU3.Notes.NotesManager::isAutoPlay()' } })
        if ($autoCalls.Count -lt 1) { throw "Native auto notes branch absent: $name" }
    }
    foreach ($spec in @(@('MU3.Notes.BellNoteCoreBase', 'checkBellHit', 'frame', 'Get', 3), @('MU3.Notes.ShellNoteCore', 'checkHit', 'frameHit', 'Dodge', 1))) {
        $type = $game.MainModule.Types | Where-Object FullName -eq $spec[0]
        $method = @($type.Methods | Where-Object { $_.Name -eq $spec[1] -and $_.Parameters.Count -eq 0 })
        if ($method.Count -ne 1 -or $method[0].ReturnType.FullName -ne ($spec[0] + '/Result')) { throw "Automation signature mismatch: $($spec[0])" }
        $getter = $type.Methods | Where-Object Name -eq 'get_ntMgr'
        if (!$getter -or $getter.ReturnType.FullName -ne 'MU3.Notes.NotesManager') { throw 'NotesManager accessor mismatch' }
        $param = $type.NestedTypes | Where-Object Name -eq 'Param'
        $frameField = $param.Fields | Where-Object { $_.Name -eq $spec[2] -and $_.FieldType.FullName -eq 'System.Single' -and $_.IsPublic }
        if (!$frameField) { throw 'Scheduled frame field mismatch' }
        $enum = $type.NestedTypes | Where-Object Name -eq 'Result'
        $value = $enum.Fields | Where-Object Name -eq $spec[3]
        if (!$value -or $value.Constant -ne $spec[4]) { throw 'Automation enum mismatch' }
        $checkPlay = $type.Methods | Where-Object Name -eq 'checkPlay'
        $scheduledCall = @($checkPlay.Body.Instructions | Where-Object { $_.Operand -and $_.Operand.ToString().Contains('::' + $spec[1] + '()') })
        if ($scheduledCall.Count -ne 1) { throw 'Automation does not feed native checkPlay' }
    }
    $xml = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $GameDir 'mu3_Data\Managed\System.Xml.dll'))
    try {
        $serializer = $xml.MainModule.Types | Where-Object FullName -eq 'System.Xml.Serialization.XmlSerializer'
        $threshold = $serializer.Fields | Where-Object { $_.Name -eq 'generationThreshold' -and $_.IsStatic -and $_.FieldType.FullName -eq 'System.Int32' }
        if (!$threshold) { throw 'Mono XML threshold signature mismatch' }
        foreach ($methodName in @('CreateReader', 'CreateWriter')) {
            $methods = @($serializer.Methods | Where-Object { $_.Name -eq $methodName -and $_.Parameters.Count -eq 1 })
            if ($methods.Count -ne 1) { throw "XML $methodName overload mismatch" }
            $instructions = $methods[0].Body.Instructions
            $found = $false
            for ($i=0; $i -lt $instructions.Count - 4; $i++) {
                if ($instructions[$i].OpCode.Name -eq 'ldsfld' -and $instructions[$i].Operand.Name -eq 'generationThreshold' -and
                    $instructions[$i+1].OpCode.Name -eq 'ldc.i4.m1' -and $instructions[$i+2].OpCode.Name -eq 'bne.un' -and
                    $instructions[$i+4].OpCode.Name -eq 'newobj' -and $instructions[$i+4].Operand.DeclaringType.Name -match 'Interpreter$') { $found = $true }
            }
            if (!$found) { throw "XML $methodName does not use interpreter for threshold -1" }
        }
    } finally { $xml.Dispose() }
    $data = $game.MainModule.Types | Where-Object FullName -eq 'MU3.Data.DataManager'
    $init = @($data.Methods | Where-Object { $_.Name -eq 'initializeDevelop' -and $_.Parameters.Count -eq 0 -and $_.ReturnType.FullName -eq 'System.Void' })
    if ($init.Count -ne 1) { throw 'Data initialization timing hook mismatch' }
    $dm = $game.MainModule.Types | Where-Object FullName -eq 'MU3.Data.DataManager'
    $analysisField = $dm.Fields | Where-Object { $_.Name -eq '_fumenAnalysisData' -and $_.FieldType.FullName -eq 'System.Collections.Generic.Dictionary`2<System.Int32,MU3.Data.FumenAnalysisData>' }
    if (!$analysisField) { throw 'Chart cache dictionary mismatch' }
    foreach ($name in @('makeFumenAnalysisDataList', 'diffID', 'getOgkrPath')) {
        if (@($dm.Methods | Where-Object Name -eq $name).Count -ne 1) { throw "Chart cache method mismatch: $name" }
    }
    $notes = $game.MainModule.Types | Where-Object FullName -eq 'MU3.Notes.NotesManager'
    foreach ($spec in @(@('_fieldStateList','MU3.Notes.NotesManager/FieldState[]'), @('_currentFieldState','System.Int32'), @('_frameFieldOff','System.Single'))) {
        if (!($notes.Fields | Where-Object { $_.Name -eq $spec[0] -and $_.FieldType.FullName -eq $spec[1] })) { throw 'Auto track field mismatch' }
    }
    $score = @($notes.Methods | Where-Object { $_.Name -eq 'setResultEffectAndScore' -and $_.Parameters.Count -eq 7 })
    if ($score.Count -ne 1 -or $score[0].Parameters[0].Name -ne 'judge' -or $score[0].Parameters[1].Name -ne 'timing' -or $score[0].Parameters[6].Name -ne 'frameDiff') { throw 'Theoretical judgment injection mismatch' }
    $gp = $game.MainModule.Types | Where-Object FullName -eq 'MU3.UIGPDialog'
    foreach ($name in @('state_','uiSelector_','content_','productList_','buttons_','enableCancel_')) {
        if (!($gp.Fields | Where-Object Name -eq $name)) { throw 'GP confirmation field mismatch' }
    }
    $selector = $game.MainModule.Types | Where-Object FullName -eq 'MU3.UISelector'
    if (!($selector.Methods | Where-Object { $_.Name -eq 'triggerDecide' -and $_.IsPublic -and $_.Parameters.Count -eq 0 })) { throw 'GP native confirmation method mismatch' }
    if (!($selector.Methods | Where-Object { $_.Name -eq 'get_selectIndexRaw' -and $_.IsPublic -and $_.ReturnType.FullName -eq 'System.Int32' })) { throw 'GP raw selection accessor mismatch' }
    if (!($selector.Methods | Where-Object { $_.Name -eq 'setSelectIndexRaw' -and $_.IsPublic -and ($_.Parameters.ParameterType.FullName -join ',') -eq 'System.Int32,System.Boolean,System.Boolean' })) { throw 'GP keyboard navigation signature mismatch' }
    $gpPrefix = ($plugin.MainModule.Types | Where-Object FullName -eq 'GekiDrive.GpConfirm').Methods | Where-Object Name -eq 'Prefix'
    if ($gpPrefix.Body.Instructions | Where-Object { $_.Operand -and $_.Operand.ToString() -eq 'System.Int32 MU3.UISelector::get_selectIndex()' }) { throw 'GP list accesses must use raw selection, not logical option index' }
    $enemy = $game.MainModule.Types | Where-Object FullName -eq 'MU3.Battle.EnemyManager'
    if (!($enemy.Fields | Where-Object Name -eq '_lifeList')) { throw 'Overdamage life ladder mismatch' }
    if (!($enemy.Methods | Where-Object { $_.Name -eq 'getWDD' -and $_.ReturnType.FullName -eq 'MU3.Notes.NotesManager/WaveDetailData' })) { throw 'Overdamage current wave accessor mismatch' }
    $attack = @($enemy.Methods | Where-Object Name -eq 'attackPlayer')
    if ($attack.Count -ne 1 -or $attack[0].Parameters[0].Name -ne 'damage' -or $attack[0].Parameters[0].ParameterType.FullName -ne 'System.Int32') { throw 'Overdamage damage argument mismatch' }
    foreach ($reference in $plugin.MainModule.AssemblyReferences) {
        $paths = @((Join-Path $GameDir ('mu3_Data\Managed\' + $reference.Name + '.dll')), (Join-Path $GameDir ('BepInEx\core\' + $reference.Name + '.dll')))
        if (!($paths | Where-Object { Test-Path -LiteralPath $_ })) { throw "Missing runtime assembly: $($reference.Name)" }
    }
    $files = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot '..\artifacts') -Filter '*.dll')
    if ($files.Count -ne 1 -or $files[0].Name -ne 'GekiDrive.Public.dll') { throw 'Unexpected redistributed dependency DLLs' }
    Write-Host 'PASS: known binary, CLR 2.0, scene signatures, unique timer call, local runtime dependencies, standalone output, AutoPlay callers, Bell/Shell timing and result enums, Beam collision/damage path, XML interpreter branches, chart cache, theoretical scoring, auto track, GP confirmation and boss damage injection signatures.'
    Write-Host 'This is static validation. In-game behavior and Harmony coexistence remain unverified.'
}
finally { $game.Dispose(); $plugin.Dispose() }





