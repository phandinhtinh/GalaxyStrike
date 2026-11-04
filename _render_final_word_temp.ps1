$ErrorActionPreference = 'Stop'
$src = 'D:\GAME\GalaxyStrike\_report_final.docx'
$pdf = 'D:\GAME\GalaxyStrike\_report_qa_final\final_word.pdf'
$word = New-Object -ComObject Word.Application
$word.Visible = $false
$word.DisplayAlerts = 0
$word.AutomationSecurity = 3
$doc = $word.Documents.Open($src, $false, $true, $false)
Write-Output 'OPENED'
$doc.ExportAsFixedFormat($pdf, 17, $false, 0, 0, 1, 1, 0, $true, $false, 1, $true, $true, $false)
Write-Output 'EXPORTED'
$doc.Close(0)
$word.Quit()
[System.Runtime.InteropServices.Marshal]::ReleaseComObject($doc) | Out-Null
[System.Runtime.InteropServices.Marshal]::ReleaseComObject($word) | Out-Null
Write-Output $pdf
