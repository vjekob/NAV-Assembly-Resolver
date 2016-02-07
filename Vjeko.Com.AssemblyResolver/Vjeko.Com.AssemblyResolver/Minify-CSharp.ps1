Function Minify-CSharpFile()
{
    Param(
        [Parameter(Mandatory = $true)] [string] $FilePath,
        [Parameter(Mandatory = $true)] [int] $LineLength
    )

    $lines = [System.IO.File]::ReadAllLines($FilePath)
    $code = ""

    foreach ($line in $lines) {
        if ([System.String]::IsNullOrWhiteSpace($line) -eq $false -and $line.Trim().StartsWith("//") -eq $false) {
            $code += $line.Trim();
        }
    }

    $code = $code.Replace(", ", ",").Replace(" :", ":").Replace(": ", ":").Replace(" = ", "=").Replace(" += ", "+=").Replace(" -= ", "-=").Replace(" != ", "!=").Replace("{ }", "{}").Replace("> ", ">").Replace(" (", "(").Replace(" {", "{").Replace("] ", "]").Replace(" && ", "&&").Replace(" =>", "=>").Replace("{ ", "{").Replace(" }", "}").Replace("; ", ";").Replace(" == ", "==").Replace(" ?", "?").Replace("? ", "?").Replace(") ", ")").Replace(" ?", "?")

    $new = New-Object "System.Collections.Generic.List[System.String]"
    do {
        if ($code.Length -le $LineLength) {
            $line = $code
            $code = ""
        } else {
            $line = $code.Substring(0, $LineLength)
            $code = $code.Substring($LineLength)
        }
        "Source += '" + $line + "';"
    } while ([System.String]::IsNullOrWhiteSpace($code) -eq $false)

    foreach ($line in $new) {
    #    $line
    }
}