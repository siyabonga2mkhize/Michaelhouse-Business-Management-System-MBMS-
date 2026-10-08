<#
  Helper used by Seed-Azure.ps1 (and testable on LocalDB). Runs ONLY the project's Seed method
  against a database. It never runs migrations and never changes the schema.

  Invoke-MbmsSeed -ConnectionString <cs> -BinDir <path to bin>
    1. checks the database schema matches the code (CompatibleWithModel); stops if it does not
    2. runs Configuration.Seed (adds rows that are missing; the seed is written to be repeatable)
#>
function Invoke-MbmsSeed {
    param([Parameter(Mandatory)][string]$ConnectionString, [Parameter(Mandatory)][string]$BinDir)

    $handler = [ResolveEventHandler]{
        param($sender, $e)
        $name = ($e.Name -split ',')[0]
        $p = Join-Path $BinDir ($name + '.dll')
        if (Test-Path $p) { return [Reflection.Assembly]::LoadFrom($p) }
        return $null
    }.GetNewClosure()
    [AppDomain]::CurrentDomain.add_AssemblyResolve($handler)
    try {
        $asm     = [Reflection.Assembly]::LoadFrom((Join-Path $BinDir 'Michaelhouse.dll'))
        $ctxType = $asm.GetType('Michaelhouse.Models.DBContextClass', $true)

        # No automatic create/migrate: we only want to read and seed
        $ef  = [Reflection.Assembly]::LoadFrom((Join-Path $BinDir 'EntityFramework.dll'))
        $dbT = $ef.GetType('System.Data.Entity.Database', $true)
        $set = $dbT.GetMethod('SetInitializer').MakeGenericMethod($ctxType)
        [void]$set.Invoke($null, @($null))

        $conn = New-Object System.Data.SqlClient.SqlConnection $ConnectionString
        $ctor = $ctxType.GetConstructor(@([System.Data.Common.DbConnection], [bool]))
        $ctx  = $ctor.Invoke([object[]]@($conn.PSObject.BaseObject, [bool]$true))
        try {
            $compatible = $ctx.Database.CompatibleWithModel($false)
            if (-not $compatible) {
                return [pscustomobject]@{ Ok = $false; Message = 'The database schema does NOT match the code (EF model check failed). Nothing was seeded.' }
            }
            $cfgType = $asm.GetType('Configuration', $true)
            $cfg     = [Activator]::CreateInstance($cfgType, $true)
            $seed    = $cfgType.GetMethod('Seed', [Reflection.BindingFlags]'Instance,NonPublic,Public,DeclaredOnly')
            [void]$seed.Invoke($cfg, @($ctx))
            return [pscustomobject]@{ Ok = $true; Message = 'Schema matches the code. Seed completed.' }
        }
        finally { $ctx.Dispose() }
    }
    catch {
        $ex = $_.Exception; while ($ex.InnerException) { $ex = $ex.InnerException }
        return [pscustomobject]@{ Ok = $false; Message = 'Seed failed: ' + $ex.Message }
    }
    finally { [AppDomain]::CurrentDomain.remove_AssemblyResolve($handler) }
}
