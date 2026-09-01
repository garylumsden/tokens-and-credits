param(
    [Parameter(Mandatory = $true)]
    [string] $SourceArchive,

    [string] $OutputPath = (
        Join-Path $PSScriptRoot "..\src\TokensAndCredits.Web\Resources\embeddings\glove-wiki-gigaword-50.top10000.txt.gz"
    )
)

$ErrorActionPreference = "Stop"

$expectedSourceMd5 = "c289bc5d7f2f02c6dc9f2f9b67641813"
$subsetSize = 10000
$dimensions = 50

$source = (Resolve-Path $SourceArchive).Path
$sourceHash = (Get-FileHash -Algorithm MD5 $source).Hash.ToLowerInvariant()
if ($sourceHash -ne $expectedSourceMd5) {
    throw "Source MD5 mismatch. Expected $expectedSourceMd5 but received $sourceHash."
}

$output = [System.IO.Path]::GetFullPath($OutputPath)
[System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($output)) | Out-Null

$sourceStream = [System.IO.File]::OpenRead($source)
$sourceGzip = [System.IO.Compression.GZipStream]::new(
    $sourceStream,
    [System.IO.Compression.CompressionMode]::Decompress
)
$reader = [System.IO.StreamReader]::new($sourceGzip, [System.Text.Encoding]::UTF8)

$outputStream = [System.IO.File]::Create($output)
$outputGzip = [System.IO.Compression.GZipStream]::new(
    $outputStream,
    [System.IO.Compression.CompressionLevel]::SmallestSize
)
$utf8WithoutBom = [System.Text.UTF8Encoding]::new($false)
$writer = [System.IO.StreamWriter]::new($outputGzip, $utf8WithoutBom)
$writer.NewLine = "`n"

try {
    $header = $reader.ReadLine()
    if ($header -ne "400000 50") {
        throw "Unexpected source header '$header'."
    }

    $writer.WriteLine("$subsetSize $dimensions")
    $seen = [System.Collections.Generic.HashSet[string]]::new(
        [System.StringComparer]::Ordinal
    )

    for ($index = 0; $index -lt $subsetSize; $index++) {
        $line = $reader.ReadLine()
        if ($null -eq $line) {
            throw "Source ended after $index vectors."
        }

        $parts = $line.Split(" ", [System.StringSplitOptions]::RemoveEmptyEntries)
        if ($parts.Length -ne ($dimensions + 1)) {
            throw "Vector '$($parts[0])' has $($parts.Length - 1) values. Expected $dimensions."
        }

        if (!$seen.Add($parts[0])) {
            throw "Duplicate word '$($parts[0])'."
        }

        foreach ($value in $parts[1..$dimensions]) {
            $parsed = 0.0
            if (![double]::TryParse(
                $value,
                [System.Globalization.NumberStyles]::Float,
                [System.Globalization.CultureInfo]::InvariantCulture,
                [ref] $parsed
            ) -or [double]::IsNaN($parsed) -or [double]::IsInfinity($parsed)) {
                throw "Vector '$($parts[0])' contains invalid value '$value'."
            }
        }

        $writer.WriteLine($line)
    }
}
finally {
    $writer.Dispose()
    $outputGzip.Dispose()
    $outputStream.Dispose()
    $reader.Dispose()
    $sourceGzip.Dispose()
    $sourceStream.Dispose()
}

$assetHash = (Get-FileHash -Algorithm SHA256 $output).Hash.ToLowerInvariant()
Write-Output "Wrote $subsetSize vectors to $output"
Write-Output "SHA-256: $assetHash"
