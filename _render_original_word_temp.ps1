$ErrorActionPreference = 'Stop'
$src = 'D:\GAME\GalaxyStrike\_report_source.docx'
$outDir = 'D:\GAME\GalaxyStrike\_report_qa_original'
New-Item -ItemType Directory -Path $outDir -Force | Out-Null
$pdf = Join-Path $outDir 'original_word.pdf'
$word = New-Object -ComObject Word.Application
$word.Visible = $false
$word.DisplayAlerts = 0
$doc = $word.Documents.Open($src, $false, $true)
$doc.ExportAsFixedFormat($pdf, 17)
$doc.Close(0)
$word.Quit()
[System.Runtime.InteropServices.Marshal]::ReleaseComObject($doc) | Out-Null
[System.Runtime.InteropServices.Marshal]::ReleaseComObject($word) | Out-Null
Write-Output $pdf
