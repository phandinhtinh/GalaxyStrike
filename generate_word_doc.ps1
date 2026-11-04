# PowerShell Script to generate LEVEL_DOCUMENTATION_GALAXY_STRIKE.docx via Word COM Automation
$ErrorActionPreference = "Stop"

$assetsDir = "D:\GAME\GalaxyStrike\Documentation_Assets"
$outputDocx = "D:\GAME\GalaxyStrike\LEVEL_DOCUMENTATION_GALAXY_STRIKE.docx"

Write-Host "Starting Word Application..."
$word = New-Object -ComObject Word.Application
$word.Visible = $false
$word.DisplayAlerts = 0

try {
    $doc = $word.Documents.Add()

    # --- PAGE SETUP ---
    $doc.PageSetup.TopMargin = 56.7    # 2.0 cm
    $doc.PageSetup.BottomMargin = 56.7 # 2.0 cm
    $doc.PageSetup.LeftMargin = 70.87  # 2.5 cm
    $doc.PageSetup.RightMargin = 56.7  # 2.0 cm

    # Base styling
    $selection = $word.Selection

    function AddPara($text, $fontName="Times New Roman", $fontSize=11, $isBold=$false, $isItalic=$false, $align=0, $spaceAfter=4, $color=0) {
        $selection.Font.Name = $fontName
        $selection.Font.Size = $fontSize
        $selection.Font.Bold = [int]$isBold
        $selection.Font.Italic = [int]$isItalic
        $selection.ParagraphFormat.Alignment = $align
        $selection.ParagraphFormat.SpaceAfter = $spaceAfter
        $selection.ParagraphFormat.LineSpacingRule = 0 # Single / 1.15
        if ($color -ne 0) { $selection.Font.Color = $color } else { $selection.Font.ColorIndex = 0 }
        $selection.TypeText($text)
        $selection.TypeParagraph()
    }

    function AddHeading1($text) {
        AddPara $text "Times New Roman" 15 $true $false 0 6 0x803000 # Deep Navy Blue
    }

    function AddHeading2($text) {
        AddPara $text "Times New Roman" 13 $true $false 0 4 0xB05000 # Steel Blue
    }

    function AddHeading3($text) {
        AddPara $text "Times New Roman" 11.5 $true $true 0 3 0x000000
    }

    function AddImage($relPath, $widthPt, $heightPt=$null, $align=1, $caption="") {
        $fullPath = Join-Path $assetsDir $relPath
        if (Test-Path $fullPath) {
            $selection.ParagraphFormat.Alignment = $align
            $selection.ParagraphFormat.SpaceAfter = 2
            $shape = $selection.InlineShapes.AddPicture($fullPath)
            if ($widthPt -gt 0) {
                if ($heightPt -ne $null -and $heightPt -gt 0) {
                    $shape.Width = $widthPt
                    $shape.Height = $heightPt
                } else {
                    $origW = $shape.Width
                    $origH = $shape.Height
                    $shape.Width = $widthPt
                    $shape.Height = $origH * ($widthPt / $origW)
                }
            }
            $selection.TypeParagraph()
            if ($caption -ne "") {
                AddPara $caption "Times New Roman" 9.5 $false $true 1 6 0x555555
            }
        } else {
            Write-Warning "Image not found: $fullPath"
        }
    }

    function FormatHeaderRow($table, $colCount, $bgHex=0xF0E0D0) {
        $table.Rows.Item(1).HeadingFormat = -1
        $table.Rows.Item(1).Range.Font.Bold = 1
        $table.Rows.Item(1).Range.Font.Size = 10
        $table.Rows.Item(1).Range.Font.Name = "Times New Roman"
        $table.Rows.Item(1).Range.ParagraphFormat.Alignment = 1 # Center
        $table.Rows.Item(1).Range.Shading.BackgroundPatternColor = $bgHex
        for ($c = 1; $c -le $colCount; $c++) {
            $table.Cell(1, $c).VerticalAlignment = 1 # Center
        }
    }

    # ==========================================================
    # 1. TRANG BÌA (COVER PAGE)
    # ==========================================================
    Write-Host "Creating Cover Page..."
    AddPara "HỌC VIỆN CÔNG NGHỆ BƯU CHÍNH VIỄN THÔNG" "Times New Roman" 13 $true $false 1 2
    AddPara "CHƯƠNG TRÌNH THIẾT KẾ VÀ PHÁT TRIỂN GAME" "Times New Roman" 12 $true $false 1 2
    AddPara "THIẾT KẾ CƠ CHẾ VÀ MÀN CHƠI" "Times New Roman" 12 $true $false 1 18

    # Logo PTIT
    AddImage "ptit_logo.png" 95 95 1 ""

    AddPara "" "Times New Roman" 11 $false $false 1 12
    AddPara "BÀI BÁO CÁO LEVEL DOCUMENTATION:" "Times New Roman" 16 $true $false 1 4 0x000080
    AddPara "Galaxy Strike: Galactic Warfare" "Times New Roman" 20 $true $false 1 36 0x803000

    # Bảng thông tin sinh viên & giảng viên
    $tblCover = $doc.Tables.Add($selection.Range, 3, 2)
    $tblCover.Borders.Enable = $false
    $tblCover.Columns.Item(1).Width = 140
    $tblCover.Columns.Item(2).Width = 260

    $coverData = @(
        @("Giảng viên hướng dẫn:", "Trần Duy Hiếu"),
        @("Lớp:", "D24CQGA04-B"),
        @("Sinh viên thực hiện:", "Hoàng Trung Hiếu - B24DCGA048")
    )
    for ($r = 0; $r -lt 3; $r++) {
        $cell1 = $tblCover.Cell($r+1, 1)
        $cell1.Range.Text = $coverData[$r][0]
        $cell1.Range.Font.Name = "Times New Roman"
        $cell1.Range.Font.Size = 11.5
        $cell1.Range.Font.Bold = 1
        $cell1.Range.ParagraphFormat.Alignment = 0

        $cell2 = $tblCover.Cell($r+1, 2)
        $cell2.Range.Text = $coverData[$r][1]
        $cell2.Range.Font.Name = "Times New Roman"
        $cell2.Range.Font.Size = 11.5
        $cell2.Range.Font.Bold = 0
        $cell2.Range.ParagraphFormat.Alignment = 0
    }

    $selection.Start = $tblCover.Range.End
    $selection.TypeParagraph()
    AddPara "" "Times New Roman" 11 $false $false 1 48
    AddPara "Hà Nội – 2026" "Times New Roman" 12 $true $false 1 0

    # Ngắt trang
    $selection.InsertBreak(7) # wdPageBreak

    # ==========================================================
    # 2. MỤC LỤC (TABLE OF CONTENTS)
    # ==========================================================
    Write-Host "Creating Table of Contents..."
    AddPara "MỤC LỤC" "Times New Roman" 16 $true $false 1 12 0x803000

    $tocItems = @(
        "I. GIỚI THIỆU GAME",
        "    1. Thông tin tổng quan",
        "    2. Concept",
        "    3. Game tồn tại để làm gì?",
        "    4. Người chơi có thể làm gì?",
        "    5. Goal & Win/Lose State",
        "II. GAMEPLAY CƠ BẢN",
        "    1. Core Loop",
        "    2. Bàn chơi / Không gian chiến trường (Playfield & Enemy Grid)",
        "    3. Các loại Phi thuyền Kẻ địch xuất hiện trên màn chơi",
        "    4. Hệ thống Hỏa lực & Cấp độ Đạn (Bullet & Firepower)",
        "    5. Cách điều khiển Phi thuyền (Controls)",
        "    6. Ràng buộc & Giới hạn",
        "    7. Booster & Vật phẩm hỗ trợ",
        "III. MECHANICS (50 LEVEL ĐẦU)",
        "IV. MỘT SỐ CHỦ ĐỀ CHIẾN TRƯỜNG (THEMES & ZONES VŨ TRỤ)",
        "V. TIẾN TRÌNH 50 LEVEL ĐẦU",
        "    1. Mục tiêu giai đoạn 50 level đầu",
        "    2. Đường cong độ khó (Difficulty Curve)",
        "    3. Lịch mở khóa Mechanic",
        "    4. Lịch mở khóa Booster",
        "    5. Các mốc UX & Monetization quan trọng",
        "    6. Level Metrics mục tiêu",
        "    7. Bảng roadmap chi tiết 50 level",
        "VI. CHI TIẾT 10 LEVEL ĐẦU (15 LEVEL ĐẦU)",
        "    1. Bảng thiết kế Level chi tiết",
        "    2. Thời gian, độ khó cảm nhận và Reward",
        "    3. Cách tính Time Set & Reward"
    )
    foreach ($item in $tocItems) {
        $isMajor = $item -match '^[I|V]'
        AddPara $item "Times New Roman" (if ($isMajor) { 11.5 } else { 10.5 }) $isMajor $false 0 (if ($isMajor) { 4 } else { 2 })
    }

    $selection.InsertBreak(7) # wdPageBreak

    # ==========================================================
    # I. GIỚI THIỆU GAME
    # ==========================================================
    Write-Host "Creating Section I..."
    AddHeading1 "I. GIỚI THIỆU GAME"

    AddHeading2 "1. Thông tin tổng quan"
    $tblOverview = $doc.Tables.Add($selection.Range, 6, 2)
    $tblOverview.Borders.Enable = $true
    $tblOverview.Columns.Item(1).Width = 120
    $tblOverview.Columns.Item(2).Width = 360
    FormatHeaderRow $tblOverview 2 0xE6F0FA

    $tblOverview.Cell(1, 1).Range.Text = "Thuộc tính"
    $tblOverview.Cell(1, 2).Range.Text = "Nội dung chi tiết"

    $ovData = @(
        @("Tên chính thức", "Galaxy Strike: Galactic Warfare"),
        @("Thể loại", "Space Shooter / Top-Down Bullet Hell Arcade (Bắn ruồi không gian hiện đại)"),
        @("Nền tảng", "Mobile (Android / iOS) + PC / WebGL"),
        @("Đối tượng người chơi", "Người chơi Casual & Mid-core yêu thích thể loại Arcade kinh điển (Galaga, Space Invaders, 1945), giải trí nhịp độ nhanh, phản xạ né đạn đã mắt."),
        @("USP (Độc đáo)", "Đội hình ma trận hạm đội (Grid Fleet System) với chuyển động tuần tra hình Sin, kết hợp thanh Máu tổng màn chơi (Stage Fleet Health Bar) và hỏa lực đạn quạt cực đại (Penta Overdrive Storm)."),
        @("Mô hình kinh doanh", "IAA (Rewarded Video hồi sinh & nhân đôi coin; Interstitial Ads sau các mốc level) + IAP (Gói Coin nâng cấp hỏa lực, mua Booster, gói gỡ quảng cáo No-Ads).")
    )
    for ($r = 0; $r -lt $ovData.Count; $r++) {
        $c1 = $tblOverview.Cell($r+2, 1)
        $c1.Range.Text = $ovData[$r][0]
        $c1.Range.Font.Bold = 1
        $c1.Range.Font.Size = 10
        $c1.Range.Font.Name = "Times New Roman"

        $c2 = $tblOverview.Cell($r+2, 2)
        $c2.Range.Text = $ovData[$r][1]
        $c2.Range.Font.Bold = 0
        $c2.Range.Font.Size = 10
        $c2.Range.Font.Name = "Times New Roman"
    }

    $selection.Start = $tblOverview.Range.End
    $selection.TypeParagraph()

    AddHeading2 "2. Concept."
    AddPara "Galaxy Strike là tựa game bắn phi thuyền không gian cuộn cảnh dọc (Vertical Space Shooter). Người chơi sẽ trực tiếp điều khiển một chiến đấu cơ không gian tối tân, di chuyển linh hoạt trên chiến trường để tiêu diệt các đợt (Waves) hạm đội địch đang bay vào dàn trận theo ma trận ô lưới (Grid Matrix)." "Times New Roman" 11 $false $false 3 4
    AddPara "Mỗi Level là một chiến dịch độc lập với sự kết hợp phong phú giữa số lượng đợt quái, chủng loại chiến hạm ngoài hành tinh, và cách bố trí đội hình. Người chơi cần tập trung hỏa lực để bào mòn thanh máu tổng của màn chơi (Stage Fleet HP) trước khi cạn kiệt lượng máu của phi thuyền. Khi toàn bộ hạm đội địch trong màn bị quét sạch hoặc máu tổng của màn chạm mốc 0, chiến dịch hoàn thành và người chơi tiến vào khu vực không gian kế tiếp." "Times New Roman" 11 $false $false 3 6

    AddHeading2 "3. Game tồn tại để làm gì?"
    AddPara "Mang lại cảm giác thư giãn, xả stress kết hợp hưng phấn kịch tính: Người chơi được đắm chìm trong không gian chiến trận ngân hà với âm thanh cháy nổ hoành tráng, phản xạ né tránh các chùm đạn đỏ và đòn bổ nhào cảm tử của kẻ địch. Mỗi phát đạn bắn trúng đều có phản hồi thị giác tức thì (kẻ địch chớp đỏ nhận sát thương, thanh Stage HP trên đỉnh màn hình tụt dần theo thời gian thực, kẻ địch phát nổ vỡ vụn kèm âm thanh uy lực và rơi tiền vàng), tạo động lực thôi thúc mạnh mẽ: 'Chơi thêm một màn nữa để nâng cấp đạn và chạm trán Siêu Trùm Mẫu Hạm!'" "Times New Roman" 11 $false $false 3 6

    AddHeading2 "4. Người chơi có thể làm gì?"
    $actions = @(
        "Touch & Drag / Swipe: Chạm và kéo rê ngón tay để điều khiển phi thuyền luồn lách mượt mà né tránh đạn và tàu địch.",
        "Auto-Fire Stream: Phi thuyền tự động xả đạn liên tục theo chu kỳ, giúp người chơi tập trung tối đa vào kỹ năng định vị né đòn và gom vật phẩm.",
        "Collect Power-Ups: Nhặt các vật phẩm nâng cấp đạn rơi ra từ xác tàu địch để tăng số lượng tia đạn từ 1 tia lên tối đa 5 tia cực đại (Penta Overdrive).",
        "Gather Coins: Nhặt các cọc tiền vàng rơi trên chiến trường để tích lũy tài nguyên mua Booster và nâng cấp phi thuyền.",
        "Deploy Boosters: Sử dụng 4 loại bổ trợ công nghệ cao (Overdrive Surge, Energy Shield, EMP Nuke, Hyper Magnet) để xoay chuyển cục diện nguy hiểm.",
        "Defeat Bosses & Clear Waves: Tiêu diệt hạm đội chỉ huy Mini-Boss và Siêu Chiến Hạm Flagship để chinh phục toàn bộ 50 màn chơi."
    )
    foreach ($act in $actions) {
        AddPara "• $act" "Times New Roman" 10.5 $false $false 0 3
    }

    AddHeading2 "5. Goal & Win/Lose State"
    AddPara "Goal: Tiêu diệt toàn bộ các Wave kẻ địch, hạ cạn kiệt thanh Máu tổng của hạm đội (Stage Fleet HP = 0) để giải phóng vùng không gian." "Times New Roman" 11 $false $false 0 3
    AddPara "Win: Dọn sạch toàn bộ số lượng quái quy định hoặc thanh Stage Fleet HP về 0 mà phi thuyền người chơi vẫn còn máu → Hoàn thành Level, nhận Coin thưởng và hiển thị Banner chúc mừng để sang màn tiếp theo." "Times New Roman" 11 $false $false 0 3
    AddPara "Lose: Phi thuyền người chơi bị trúng đạn hoặc va chạm với tàu địch đến khi cạn kiệt 3 vạch máu (Player HP = 0) → Thất bại Level (Game Over Pop-up, có thể xem quảng cáo hoặc dùng Coin để Hồi sinh tiếp tục)." "Times New Roman" 11 $false $false 0 6

    # ==========================================================
    # II. GAMEPLAY CƠ BẢN
    # ==========================================================
    Write-Host "Creating Section II..."
    AddHeading1 "II. GAMEPLAY CƠ BẢN"

    AddHeading2 "1. Core Loop."
    AddPara "Quan sát & Xuất kích → Di chuyển né đạn & Xả hỏa lực → Nhặt Power-Up/Coins → Quét sạch Wave/Hạ Boss → Nhận thưởng & Nâng cấp." "Times New Roman" 11 $true $false 1 8 0x803000

    AddHeading2 "2. Bàn chơi / Không gian chiến trường (Playfield & Enemy Grid)"
    AddPara "Cấu trúc: Dạng cuộn cảnh không gian vũ trụ tỷ lệ 9:16 (X giới hạn [-2.5, 2.5], Y giới hạn [-4.5, 4.5])." "Times New Roman" 11 $false $false 0 2
    AddPara "• Đội hình kẻ địch (Enemy Grid Matrix): Ma trận hàng và cột (tối đa 4 hàng x 5 cột) xuất phát từ đỉnh màn hình và dàn trận theo tọa độ không gian." "Times New Roman" 10.5 $false $false 0 2
    AddPara "• Ngưỡng an toàn (Safe Patrol Boundary: Min Y >= 1.2): Đội hình bay lượn và lắc lư (Sway) ở nửa trên màn hình, dừng lại ở ngưỡng an toàn để không trôi đè lên phi thuyền người chơi ở đáy (Y = -3.0)." "Times New Roman" 10.5 $false $false 0 2
    AddPara "• Giao diện HUD & Stage HP Bar: Đặt cố định ở đỉnh màn hình, hiển thị Tên Màn chơi, Khu vực (Zone), Điểm số, Tiền vàng, và tỷ lệ máu tổng còn lại của hạm đội địch." "Times New Roman" 10.5 $false $false 0 6

    AddImage "playfield_space.png" 270 $null 1 "Hình 2.1: Không gian chiến trường Galaxy Strike: Ma trận kẻ địch tuần tra, thanh Stage HP trên đỉnh và Phi thuyền ở đáy"

    AddHeading2 "3. Các loại Phi thuyền Kẻ địch xuất hiện trên màn chơi"
    $tblEnemies = $doc.Tables.Add($selection.Range, 10, 7)
    $tblEnemies.Borders.Enable = $true
    FormatHeaderRow $tblEnemies 7 0xE6F0FA

    $enHeaders = @("STT", "Tên Kẻ địch", "HP", "Điểm", "Coin", "Phân lớp", "Đặc tính chiến đấu")
    for ($c = 0; $c -lt 7; $c++) { $tblEnemies.Cell(1, $c+1).Range.Text = $enHeaders[$c] }

    $enData = @(
        @("1", "Enemy_Scout", "1", "100", "1", "Tier 1 (Light)", "Tàu trinh sát tốc độ cao, máu mỏng, dàn hàng mở đầu các wave."),
        @("2", "Enemy_Fighter", "2", "150", "2", "Tier 1 (Light)", "Chiến đấu cơ tiêu chuẩn, giữ cự ly đội hình tuần tra ổn định."),
        @("3", "Enemy_Striker", "2", "200", "2", "Tier 1 (Light)", "Tàu xung kích cơ động, có xu hướng bứt tốc đánh chặn."),
        @("4", "Enemy_Gunship", "3", "300", "2", "Tier 2 (Medium)", "Pháo hạm trang bị súng tự động xả đạn plasma đỏ xuống dưới."),
        @("5", "Enemy_Cruiser", "5", "500", "3", "Tier 2 (Medium)", "Tuần dương hạm bọc thép, đóng vai trò khiên chắn cho đội hình."),
        @("6", "Enemy_Destroyer", "8", "800", "5", "Tier 3 (Heavy)", "Khu trục hạm hạng nặng, chống chịu nhiều phát bắn liên tiếp."),
        @("7", "Enemy_Elite", "12", "1200", "8", "Tier 3 (Heavy)", "Chiến hạm tinh nhuệ, vừa bọc thép vừa làm cận vệ cho chỉ huy."),
        @("8", "Enemy_MiniBoss", "30", "3000", "20", "Commander", "Chỉ huy tiểu đoàn xuất hiện ở Wave cuối các màn mốc 5."),
        @("9", "Enemy_Boss", "75", "6000", "50", "Major Boss", "Đại Trùm Siêu Mẫu Hạm Flagship xuất hiện tại các màn mốc 10.")
    )
    for ($r = 0; $r -lt 9; $r++) {
        for ($c = 0; $c -lt 7; $c++) {
            $cell = $tblEnemies.Cell($r+2, $c+1)
            $cell.Range.Text = $enData[$r][$c]
            $cell.Range.Font.Name = "Times New Roman"
            $cell.Range.Font.Size = 9
            if ($c -eq 0 -or $c -eq 2 -or $c -eq 3 -or $c -eq 4) {
                $cell.Range.ParagraphFormat.Alignment = 1
            } elseif ($c -eq 1 -or $c -eq 5) {
                $cell.Range.Font.Bold = 1
            }
        }
    }

    $selection.Start = $tblEnemies.Range.End
    $selection.TypeParagraph()
    AddImage "enemy_types_grid.png" 430 $null 1 "Hình 2.2: Bảng tra cứu 9 loại phi thuyền kẻ địch xuất hiện trong Galaxy Strike"

    AddHeading2 "4. Hệ thống Hỏa lực & Cấp độ Đạn (Bullet & Firepower)"
    AddPara "Phi thuyền người chơi tự động phun đạn liên tục với chu kỳ xả cực nhanh (fireRate = 0.15s). Tiêu diệt quái có 15% cơ hội rơi vật phẩm Power-Up tăng đạn theo 5 cấp độ hỏa lực:" "Times New Roman" 11 $false $false 0 3
    AddPara "• Level 1 (Single Beam): 1 tia đạn plasma xanh chính diện, hỏa lực tập trung đơn mục tiêu." "Times New Roman" 10.5 $false $false 0 2
    AddPara "• Level 2 (Twin Cannons): 2 tia song song song hành cách nhau 0.25 đơn vị, mở rộng độ phủ quét đạn." "Times New Roman" 10.5 $false $false 0 2
    AddPara "• Level 3 (Triple Spread): 3 tia xòe góc cánh quạt nhẹ (từ -5° đến +5°), vừa quét thẳng vừa bảo vệ sườn." "Times New Roman" 10.5 $false $false 0 2
    AddPara "• Level 4 (Quad Barrage): 4 tia hỏa lực mật độ cao (từ -7.5° đến +7.5°), xuyên phá tốp quái dày đặc." "Times New Roman" 10.5 $false $false 0 2
    AddPara "• Level 5 (Overdrive Storm): 5 tia cực đại xòe góc rộng (từ -10° đến +10°), phủ trọn toàn bộ bề ngang màn hình." "Times New Roman" 10.5 $false $false 0 6

    AddImage "firepower_bullet_levels.png" 430 $null 1 "Hình 2.3: Sơ đồ 5 cấp độ hỏa lực phi thuyền từ 1 tia đơn đến 5 tia cực đại Fan-Spread"

    AddHeading2 "5. Cách điều khiển Phi thuyền (Controls)"
    AddPara "Touch & Drag (Cảm ứng vuốt): Chạm vào màn hình và kéo rê theo hướng muốn di chuyển (thao tác trực tiếp bám ngón tay)." "Times New Roman" 10.5 $false $false 0 2
    AddPara "Thả tay (Release): Khi kết thúc thao tác vuốt, phi thuyền dừng ngay tại vị trí tọa độ đó." "Times New Roman" 10.5 $false $false 0 2
    AddPara "Không ra ngoài màn hình: Khi chạm rìa giới hạn không gian, phi thuyền tự động bị chặn lại an toàn." "Times New Roman" 10.5 $false $false 0 2
    AddPara "Hỗ trợ Phím PC: Sử dụng WASD hoặc cụm 4 phím Mũi tên để di chuyển mượt mà trên máy tính." "Times New Roman" 10.5 $false $false 0 6

    AddHeading2 "6. Ràng buộc & Giới hạn."
    AddPara "Giới hạn bàn chơi: Phi thuyền chỉ được di chuyển trong phạm vi màn hình (moveBounds [-2.5, 2.5] và [-4.5, 4.5])." "Times New Roman" 10.5 $false $false 0 2
    AddPara "Giới hạn máu người chơi: Tối đa 3 Máu (maxHealth = 3). Trúng đạn hoặc va chạm mất 1 máu, bất tử nhấp nháy 1.5 giây." "Times New Roman" 10.5 $false $false 0 2
    AddPara "Giới hạn cấp hỏa lực: Cấp độ đạn tối đa có thể nâng cấp trong trận là Level 5 (maxBulletLevel = 5)." "Times New Roman" 10.5 $false $false 0 2
    AddPara "Giới hạn thời gian: Mỗi Level có thời gian nhất định (Time Set). Hết giờ trước khi dọn sạch hạm đội → Lose." "Times New Roman" 10.5 $false $false 0 6

    AddHeading2 "7. Booster."
    $tblBooster = $doc.Tables.Add($selection.Range, 5, 3)
    $tblBooster.Borders.Enable = $true
    FormatHeaderRow $tblBooster 3 0xE6F0FA
    $tblBooster.Columns.Item(1).Width = 120
    $tblBooster.Columns.Item(2).Width = 280
    $tblBooster.Columns.Item(3).Width = 80

    $tblBooster.Cell(1, 1).Range.Text = "Booster"
    $tblBooster.Cell(1, 2).Range.Text = "Tác dụng"
    $tblBooster.Cell(1, 3).Range.Text = "Giá (coin)"

    $bstData = @(
        @("Bullet Overdrive", "Tối đa hóa ngay lập tức hỏa lực lên Level 5 (5 tia cực đại) trong suốt màn chơi.", "300"),
        @("Energy Shield", "Kích hoạt màn chắn năng lượng bất tử trong 5 giây, miễn nhiễm mọi sát thương.", "350"),
        @("EMP Nuke", "Kích nổ sóng xung điện từ xóa sạch toàn bộ đạn địch và gây 50 sát thương diện rộng.", "400"),
        @("Hyper Magnet", "Tạo trường siêu dẫn tự động hút toàn bộ Coin rơi và hộp Power-Up trên chiến trường.", "450")
    )
    for ($r = 0; $r -lt 4; $r++) {
        $tblBooster.Cell($r+2, 1).Range.Text = $bstData[$r][0]
        $tblBooster.Cell($r+2, 1).Range.Font.Bold = 1
        $tblBooster.Cell($r+2, 2).Range.Text = $bstData[$r][1]
        $tblBooster.Cell($r+2, 3).Range.Text = $bstData[$r][2]
        $tblBooster.Cell($r+2, 3).Range.ParagraphFormat.Alignment = 1
        for ($c = 1; $c -le 3; $c++) {
            $tblBooster.Cell($r+2, $c).Range.Font.Name = "Times New Roman"
            $tblBooster.Cell($r+2, $c).Range.Font.Size = 10
        }
    }

    $selection.Start = $tblBooster.Range.End
    $selection.TypeParagraph()
    AddImage "boosters_diagram.png" 430 $null 1 "Hình 2.4: Sơ đồ minh họa 4 Booster vũ trụ Galaxy Strike"

    # ==========================================================
    # III. MECHANICS (50 LEVEL ĐẦU)
    # ==========================================================
    Write-Host "Creating Section III..."
    $selection.InsertBreak(7)
    AddHeading1 "III. MECHANICS (50 Level đầu)"

    $tblMech = $doc.Tables.Add($selection.Range, 7, 5)
    $tblMech.Borders.Enable = $true
    FormatHeaderRow $tblMech 5 0xFFF2CC
    $tblMech.Columns.Item(1).Width = 35
    $tblMech.Columns.Item(2).Width = 90
    $tblMech.Columns.Item(3).Width = 115
    $tblMech.Columns.Item(4).Width = 150
    $tblMech.Columns.Item(5).Width = 90

    $mHeaders = @("STT", "Mechanic", "Text Unlock", "Mô tả", "Hình ảnh")
    for ($c = 0; $c -lt 5; $c++) { $tblMech.Cell(1, $c+1).Range.Text = $mHeaders[$c] }

    $mechData = @(
        @{ stt="1"; name="Formation Patrol & Sway`n(Unlock lv 1)"; text="'Maintain flight formation! Dodge hostile trajectories and clear the vanguard!'"; desc="Đội hình kết nối thành ma trận ô lưới, lắc lư hình Sin sang 2 bên và trôi xuống trục Y nhưng dừng ở ngưỡng an toàn (Y=1.2)."; img="mech_1_formation_sway.png" },
        @{ stt="2"; name="Kamikaze Dive Attack`n(Unlock lv 5)"; text="'Warning! Enemy interceptors breaking formation to execute dive runs!'"; desc="Phi thuyền Striker tách đội hình, lao xuống theo đường parabol nhắm thẳng vào người chơi, đòi hỏi phản xạ né tránh khẩn cấp."; img="mech_2_kamikaze_dive.png" },
        @{ stt="3"; name="Hostile Counter-Fire`n(Unlock lv 12)"; text="'Alert! Enemy vessels are opening fire! Dodge incoming plasma bolts!'"; desc="Các tàu Gunship/Cruiser bắt đầu nã các viên đạn plasma đỏ xuống dưới với tần suất ngẫu nhiên, tạo áp lực vừa bắn vừa né."; img="mech_3_hostile_fire.png" },
        @{ stt="4"; name="Heavy Armor Deflector`n(Unlock lv 21)"; text="'Heavy armor detected! High durability vessels require concentrated fire!'"; desc="Chiến hạm hạng nặng (Destroyer HP 8, Elite HP 12) có giáp dày, chớp đỏ khi trúng đạn, đòi hỏi tập trung hỏa lực liên tục."; img="mech_4_heavy_armor.png" },
        @{ stt="5"; name="Squadron Commander`n(Unlock lv 31)"; text="'Commander in sight! Eliminate elite escorts to breach command defenses!'"; desc="Xuất hiện ở Wave 3 các màn mốc 5. Gồm 1-2 Mini-Boss (HP 30) đi cùng đội hình hộ vệ tinh nhuệ, vừa xả đạn vừa che chắn nhau."; img="mech_5_miniboss_commander.png" },
        @{ stt="6"; name="Flagship Mega Boss`n(Unlock lv 41)"; text="'Mothership Flagship incoming! Deplete the fleet core HP to secure total victory!'"; desc="Xuất hiện ở màn Major Boss (10, 20, 30, 40, 50). Siêu chiến hạm Flagship (HP 75+) liên kết thanh Stage HP, xả đạn diện rộng."; img="mech_6_mothership_boss.png" }
    )

    for ($r = 0; $r -lt 6; $r++) {
        $m = $mechData[$r]
        $rowIdx = $r + 2
        
        $tblMech.Cell($rowIdx, 1).Range.Text = $m.stt
        $tblMech.Cell($rowIdx, 1).Range.ParagraphFormat.Alignment = 1
        
        $tblMech.Cell($rowIdx, 2).Range.Text = $m.name
        $tblMech.Cell($rowIdx, 2).Range.Font.Bold = 1
        
        $tblMech.Cell($rowIdx, 3).Range.Text = $m.text
        $tblMech.Cell($rowIdx, 3).Range.Font.Italic = 1
        
        $tblMech.Cell($rowIdx, 4).Range.Text = $m.desc
        
        for ($c = 1; $c -le 4; $c++) {
            $tblMech.Cell($rowIdx, $c).Range.Font.Name = "Times New Roman"
            $tblMech.Cell($rowIdx, $c).Range.Font.Size = 9
        }

        # Chèn ảnh vào ô cột 5
        $cellImg = $tblMech.Cell($rowIdx, 5)
        $cellImg.Range.ParagraphFormat.Alignment = 1
        $imgFullPath = Join-Path $assetsDir $m.img
        if (Test-Path $imgFullPath) {
            $sh = $cellImg.Range.InlineShapes.AddPicture($imgFullPath)
            $sh.Width = 80
            $sh.Height = 96
        }
    }

    $selection.Start = $tblMech.Range.End
    $selection.TypeParagraph()

    # ==========================================================
    # IV. MỘT SỐ CHỦ ĐỀ CHIẾN TRƯỜNG (THEMES & ZONES VŨ TRỤ)
    # ==========================================================
    Write-Host "Creating Section IV..."
    $selection.InsertBreak(7)
    AddHeading1 "IV. MỘT SỐ CHỦ ĐỀ CHIẾN TRƯỜNG (THEMES & ZONES VŨ TRỤ)"
    AddPara "Trong suốt 50 màn chơi, chiến trường được chia thành 5 Chiến Khu Không Gian (Zones) rộng lớn. Mỗi Zone sở hữu bối cảnh thiên hà đặc trưng cùng các lớp phi thuyền kẻ địch riêng biệt:" "Times New Roman" 11 $false $false 0 6

    $zoneImgs = @(
        @{ title="1. Zone 1: Earth Orbit (Quỹ đạo Trái Đất - Level 1 đến 10)"; file="theme_zone1_earth_orbit.png" },
        @{ title="2. Zone 2: Asteroid Belt (Vành đai Tiểu hành tinh - Level 11 đến 20)"; file="theme_zone2_asteroid_belt.png" },
        @{ title="3. Zone 3: Deep Space (Không gian sâu thẳm - Level 21 đến 30)"; file="theme_zone3_deep_space.png" },
        @{ title="4. Zone 4: Alien Void (Vực thẳm Ngoại tộc - Level 31 đến 40)"; file="theme_zone4_alien_void.png" },
        @{ title="5. Zone 5: Mothership Core (Lõi Mẫu hạm Tối thượng - Level 41 đến 50)"; file="theme_zone5_mothership_core.png" }
    )
    foreach ($zi in $zoneImgs) {
        AddHeading2 $zi.title
        AddImage $zi.file 430 $null 1 ""
    }

    AddPara "Ngoài ra, các level đầu còn xen kẽ các đợt quái đặc nhiệm (Scout Rush, Heavy Cruiser Raid) và Wave Mix – phối hợp quái từ nhiều Zone để tăng độ biến ảo cho màn chơi." "Times New Roman" 10.5 $false $true 0 8

    # ==========================================================
    # V. TIẾN TRÌNH 50 LEVEL ĐẦU
    # ==========================================================
    Write-Host "Creating Section V..."
    $selection.InsertBreak(7)
    AddHeading1 "V. TIẾN TRÌNH 50 LEVEL ĐẦU"

    AddHeading2 "1. Mục tiêu giai đoạn 50 level đầu."
    AddPara "• Giới thiệu tuần tự 6 mechanic lõi và 4 booster, mỗi lần chỉ 1 thứ mới để người chơi kịp làm quen trước khi gặp thử thách tiếp theo." "Times New Roman" 10.5 $false $false 0 2
    AddPara "• Đổi Theme/Zone định kỳ sau mỗi 10 Level để tạo cảm giác 'hành trình viễn chinh ngân hà' khám phá không gian sâu thẳm." "Times New Roman" 10.5 $false $false 0 2
    AddPara "• Cài các mốc thưởng/ưu đãi (x2 coin, tặng Unlimited Life 15 phút) ngay trước hoặc sau các điểm khó, nhằm giữ chân người chơi ở giai đoạn dễ rời bỏ (churn) nhất." "Times New Roman" 10.5 $false $false 0 6

    AddHeading2 "2. Đường cong độ khó (Difficulty Curve)."
    AddPara "Độ khó trong 50 level đầu được phân bố theo dạng 'răng cưa': Easy chiếm phần lớn số level, Hard xuất hiện định kỳ mỗi ~10 level (14, 24, 34, 44) đóng vai trò 'mốc kiểm tra kỹ năng', Super Hard/Major Boss xuất hiện ở cuối chặng (10, 19, 20, 29, 30, 39, 40, 49, 50) đóng vai trò 'cao trào'. Từ level 41 trở đi, Medium bắt đầu xuất hiện xen kẽ (41, 45, 48, 50) để làm mềm độ khó tổng thể." "Times New Roman" 11 $false $false 3 6

    $tblCurve = $doc.Tables.Add($selection.Range, 4, 3)
    $tblCurve.Borders.Enable = $true
    FormatHeaderRow $tblCurve 3 0xE6F0FA
    $tblCurve.Columns.Item(1).Width = 100
    $tblCurve.Columns.Item(2).Width = 130
    $tblCurve.Columns.Item(3).Width = 250

    $tblCurve.Cell(1, 1).Range.Text = "Mốc độ khó"
    $tblCurve.Cell(1, 2).Range.Text = "Các level"
    $tblCurve.Cell(1, 3).Range.Text = "Vai trò"

    $curveData = @(
        @("Hard", "14, 24, 34, 44", "Mốc kiểm tra kỹ năng sau mỗi 10 level, không quá khắc nghiệt"),
        @("Super Hard", "10, 19, 20, 29, 30, 39, 40, 49, 50", "Cao trào của mỗi chặng, ngay trước khi đổi Theme/mở mechanic mới"),
        @("Medium", "41, 45, 48, 50", "Làm mềm nhịp độ khó ở nửa sau, chuẩn bị chuyển chặng đại chiến cuối cùng")
    )
    for ($r = 0; $r -lt 3; $r++) {
        for ($c = 0; $c -lt 3; $c++) {
            $cell = $tblCurve.Cell($r+2, $c+1)
            $cell.Range.Text = $curveData[$r][$c]
            $cell.Range.Font.Name = "Times New Roman"
            $cell.Range.Font.Size = 10
            if ($c -eq 0) { $cell.Range.Font.Bold = 1 }
        }
    }

    $selection.Start = $tblCurve.Range.End
    $selection.TypeParagraph()
    AddImage "difficulty_curve_50_levels.png" 430 $null 1 "Hình 5.1: Đường cong độ khó 50 level đầu của Galaxy Strike"

    AddHeading2 "3. Lịch mở khóa Mechanic."
    $tblMechUnlock = $doc.Tables.Add($selection.Range, 7, 3)
    $tblMechUnlock.Borders.Enable = $true
    FormatHeaderRow $tblMechUnlock 3 0xE6F0FA
    $tblMechUnlock.Columns.Item(1).Width = 70
    $tblMechUnlock.Columns.Item(2).Width = 160
    $tblMechUnlock.Columns.Item(3).Width = 250

    $tblMechUnlock.Cell(1, 1).Range.Text = "Level"
    $tblMechUnlock.Cell(1, 2).Range.Text = "Mechanic"
    $tblMechUnlock.Cell(1, 3).Range.Text = "Text Unlock (in-game)"

    $muData = @(
        @("1", "Formation Patrol & Sway", "'Maintain flight formation! Dodge hostile trajectories!'"),
        @("5", "Kamikaze Dive Attack", "'Warning! Enemy interceptors breaking formation to execute dive runs!'"),
        @("12", "Hostile Counter-Fire", "'Alert! Enemy vessels are opening fire! Dodge incoming bolts!'"),
        @("21", "Heavy Armor Deflector", "'Heavy armor detected! High durability vessels require concentrated fire!'"),
        @("31", "Squadron Commander", "'Commander in sight! Eliminate elite escorts to breach defenses!'"),
        @("41", "Flagship Mega Boss & Stage HP", "'Mothership Flagship incoming! Deplete fleet core HP for total victory!'")
    )
    for ($r = 0; $r -lt 6; $r++) {
        $tblMechUnlock.Cell($r+2, 1).Range.Text = $muData[$r][0]
        $tblMechUnlock.Cell($r+2, 1).Range.ParagraphFormat.Alignment = 1
        $tblMechUnlock.Cell($r+2, 2).Range.Text = $muData[$r][1]
        $tblMechUnlock.Cell($r+2, 2).Range.Font.Bold = 1
        $tblMechUnlock.Cell($r+2, 3).Range.Text = $muData[$r][2]
        $tblMechUnlock.Cell($r+2, 3).Range.Font.Italic = 1
        for ($c = 1; $c -le 3; $c++) {
            $tblMechUnlock.Cell($r+2, $c).Range.Font.Name = "Times New Roman"
            $tblMechUnlock.Cell($r+2, $c).Range.Font.Size = 9.5
        }
    }

    $selection.Start = $tblMechUnlock.Range.End
    $selection.TypeParagraph()

    AddHeading2 "4. Lịch mở khóa Booster."
    $tblBstUnlock = $doc.Tables.Add($selection.Range, 5, 2)
    $tblBstUnlock.Borders.Enable = $true
    FormatHeaderRow $tblBstUnlock 2 0xE6F0FA
    $tblBstUnlock.Columns.Item(1).Width = 100
    $tblBstUnlock.Columns.Item(2).Width = 380

    $tblBstUnlock.Cell(1, 1).Range.Text = "Level"
    $tblBstUnlock.Cell(1, 2).Range.Text = "Booster mở khóa"

    $buData = @(
        @("5", "Bullet Overdrive (#1) - Tối đa hóa tức thì hỏa lực lên 5 tia"),
        @("12", "Energy Shield (#2) - Màn khiên bất tử trong 5 giây"),
        @("17", "EMP Nuke (#3) - Xóa đạn địch và hủy diệt 50 sát thương toàn màn hình"),
        @("23", "Hyper Magnet (#4) - Tự động hút toàn bộ Coin và Power-Up")
    )
    for ($r = 0; $r -lt 4; $r++) {
        $tblBstUnlock.Cell($r+2, 1).Range.Text = $buData[$r][0]
        $tblBstUnlock.Cell($r+2, 1).Range.ParagraphFormat.Alignment = 1
        $tblBstUnlock.Cell($r+2, 2).Range.Text = $buData[$r][1]
        for ($c = 1; $c -le 2; $c++) {
            $tblBstUnlock.Cell($r+2, $c).Range.Font.Name = "Times New Roman"
            $tblBstUnlock.Cell($r+2, $c).Range.Font.Size = 10
        }
    }

    $selection.Start = $tblBstUnlock.Range.End
    $selection.TypeParagraph()

    AddHeading2 "5. Các mốc UX & Monetization quan trọng."
    AddPara "Level 14: Thắng level nhận thưởng x2 coin (116 coin) – đặt ngay trước khi quái bọc thép dày ra mắt ở Zone 2, tạo động lực vượt qua level Hard đầu tiên." "Times New Roman" 10.5 $false $false 0 2
    AddPara "Level 19: Tặng Unlimited Life 15 phút – an ủi ngay sau Super Hard đầu tiên, giảm nguy cơ rời bỏ (churn) ở điểm khó nhất từ đầu game." "Times New Roman" 10.5 $false $false 0 2
    AddPara "Level 20: Hiện quảng cáo Interstitial đầu tiên – đặt ngay sau mốc ưu đãi ở level 19, khi người chơi đang có thiện cảm với game, giảm cảm giác khó chịu khi gặp quảng cáo lần đầu." "Times New Roman" 10.5 $false $false 0 2
    AddPara "Level 50: Ghi chú thiết kế nội bộ: 'Độ khó Boss Mothership Core cao, cân nhắc playtest lại nhịp độ nhả đạn và hồi sinh để tối ưu hóa tỷ lệ chuyển đổi IAP gói hỗ trợ.'" "Times New Roman" 10.5 $false $false 0 6

    AddHeading2 "6. Level Metrics mục tiêu."
    $tblMetrics = $doc.Tables.Add($selection.Range, 5, 3)
    $tblMetrics.Borders.Enable = $true
    FormatHeaderRow $tblMetrics 3 0xE6F0FA
    $tblMetrics.Columns.Item(1).Width = 120
    $tblMetrics.Columns.Item(2).Width = 180
    $tblMetrics.Columns.Item(3).Width = 180

    $tblMetrics.Cell(1, 1).Range.Text = "Độ khó"
    $tblMetrics.Cell(1, 2).Range.Text = "Win rate lượt đầu"
    $tblMetrics.Cell(1, 3).Range.Text = "Churn tối đa tại level"

    $metData = @(
        @("Easy", "85% – 95%", "≤ 3%"),
        @("Medium", "65% – 75%", "≤ 5%"),
        @("Hard", "45% – 55%", "≤ 8%"),
        @("Super Hard", "30% – 40%", "≤ 8% (có quà an ủi ở level 19)")
    )
    for ($r = 0; $r -lt 4; $r++) {
        for ($c = 0; $c -lt 3; $c++) {
            $cell = $tblMetrics.Cell($r+2, $c+1)
            $cell.Range.Text = $metData[$r][$c]
            $cell.Range.Font.Name = "Times New Roman"
            $cell.Range.Font.Size = 10
            if ($c -eq 0) { $cell.Range.Font.Bold = 1 }
            if ($c -gt 0) { $cell.Range.ParagraphFormat.Alignment = 1 }
        }
    }

    $selection.Start = $tblMetrics.Range.End
    $selection.TypeParagraph()

    # --- BẢNG ROADMAP CHI TIẾT 50 LEVEL ---
    AddHeading2 "7. Bảng roadmap chi tiết 50 level."
    $tblRoadmap = $doc.Tables.Add($selection.Range, 51, 4)
    $tblRoadmap.Borders.Enable = $true
    FormatHeaderRow $tblRoadmap 4 0x203864
    $tblRoadmap.Rows.Item(1).Range.Font.Color = 0xFFFFFF
    $tblRoadmap.Columns.Item(1).Width = 50
    $tblRoadmap.Columns.Item(2).Width = 80
    $tblRoadmap.Columns.Item(3).Width = 75
    $tblRoadmap.Columns.Item(4).Width = 275

    $tblRoadmap.Cell(1, 1).Range.Text = "Level"
    $tblRoadmap.Cell(1, 2).Range.Text = "Difficulty"
    $tblRoadmap.Cell(1, 3).Range.Text = "Coin thưởng"
    $tblRoadmap.Cell(1, 4).Range.Text = "Sự kiện / Ghi chú"

    # Định nghĩa 50 level data
    $rmData = @(
        @("1", "Easy", "32", "Mở đầu chiến dịch Zone 1: Earth Orbit. Làm quen kéo tàu và bắn 1 tia"),
        @("2", "Easy", "34", "–"),
        @("3", "Easy", "36", "–"),
        @("4", "Easy", "38", "–"),
        @("5", "Hard", "105", "Mở Booster #1 – Bullet Overdrive; Mở Mechanic #2 – Kamikaze Dive"),
        @("6", "Easy", "42", "–"),
        @("7", "Easy", "44", "–"),
        @("8", "Easy", "46", "–"),
        @("9", "Easy", "48", "–"),
        @("10", "Super Hard", "250", "Đại chiến Flagship Boss đầu tiên của Zone 1"),
        @("11", "Easy", "52", "Chuyển sang Zone 2: Asteroid Belt"),
        @("12", "Easy", "54", "Mở Booster #2 – Energy Shield; Mở Mechanic #3 – Hostile Counter-Fire"),
        @("13", "Easy", "56", "–"),
        @("14", "Hard", "116", "Thắng level nhận x2 coin (116 coin)"),
        @("15", "Easy", "155", "Mini-Boss #2 – Chỉ huy tiểu đoàn có hộ vệ tinh nhuệ"),
        @("16", "Easy", "62", "–"),
        @("17", "Easy", "64", "Mở Booster #3 – EMP Nuke"),
        @("18", "Easy", "66", "–"),
        @("19", "Super Hard", "120", "Tặng Unlimited Life 15 phút sau khi vượt qua"),
        @("20", "Super Hard", "350", "Đại chiến Boss Zone 2; Sau khi thắng, hiện quảng cáo Interstitial đầu tiên"),
        @("21", "Easy", "72", "Chuyển sang Zone 3: Deep Space; Mở Mechanic #4 – Heavy Armor Deflector"),
        @("22", "Easy", "74", "–"),
        @("23", "Easy", "76", "Mở Booster #4 – Hyper Magnet"),
        @("24", "Hard", "90", "–"),
        @("25", "Easy", "205", "Mini-Boss #3 – Chỉ huy tiểu đoàn Zone 3"),
        @("26", "Easy", "82", "–"),
        @("27", "Easy", "84", "–"),
        @("28", "Easy", "86", "–"),
        @("29", "Super Hard", "120", "–"),
        @("30", "Super Hard", "450", "Đại chiến Boss Zone 3: Deep Space Mothership"),
        @("31", "Easy", "92", "Chuyển sang Zone 4: Alien Void; Mở Mechanic #5 – Squadron Commander"),
        @("32", "Easy", "94", "–"),
        @("33", "Easy", "96", "–"),
        @("34", "Hard", "90", "–"),
        @("35", "Easy", "255", "Mini-Boss #4 – Đội chỉ huy Alien Void"),
        @("36", "Easy", "102", "–"),
        @("37", "Easy", "104", "–"),
        @("38", "Easy", "106", "–"),
        @("39", "Super Hard", "120", "–"),
        @("40", "Super Hard", "550", "Đại chiến Boss Zone 4: Void Dreadnought"),
        @("41", "Medium", "112", "Chuyển sang Zone 5: Mothership Core; Mở Mechanic #6 – Flagship Boss"),
        @("42", "Easy", "114", "–"),
        @("43", "Easy", "116", "–"),
        @("44", "Hard", "90", "–"),
        @("45", "Medium", "305", "Mini-Boss #5 – Vệ binh tối cao bảo vệ lò phản ứng"),
        @("46", "Easy", "122", "–"),
        @("47", "Easy", "124", "–"),
        @("48", "Medium", "126", "–"),
        @("49", "Super Hard", "120", "–"),
        @("50", "Super Hard", "750", "Đại chiến cuối cùng: Phá hủy Lõi Mẫu Hạm Mothership Core (Cần playtest)")
    )

    for ($r = 0; $r -lt 50; $r++) {
        $rowIdx = $r + 2
        $row = $rmData[$r]
        for ($c = 0; $c -lt 4; $c++) {
            $cell = $tblRoadmap.Cell($rowIdx, $c+1)
            $cell.Range.Text = $row[$c]
            $cell.Range.Font.Name = "Times New Roman"
            $cell.Range.Font.Size = 9
            if ($c -eq 0 -or $c -eq 2) { $cell.Range.ParagraphFormat.Alignment = 1 }
            if ($c -eq 1) {
                if ($row[1] -match "Super Hard") {
                    $cell.Range.Font.Bold = 1
                    $cell.Range.Font.Color = 0x0000CC # Red
                } elseif ($row[1] -match "Hard") {
                    $cell.Range.Font.Bold = 1
                    $cell.Range.Font.Color = 0x0066CC # Orange
                } elseif ($row[1] -match "Medium") {
                    $cell.Range.Font.Color = 0x008888
                } else {
                    $cell.Range.Font.Color = 0x008800 # Green
                }
            }
        }
    }

    $selection.Start = $tblRoadmap.Range.End
    $selection.TypeParagraph()

    # ==========================================================
    # VI. CHI TIẾT 10 LEVEL ĐẦU (15 LEVEL ĐẦU)
    # ==========================================================
    Write-Host "Creating Section VI..."
    $selection.InsertBreak(7)
    AddHeading1 "VI. CHI TIẾT 10 LEVEL ĐẦU (15 LEVEL ĐẦU)"

    AddHeading2 "1. Bảng thiết kế Level."
    AddPara "Bảng dưới đây trình bày đầy đủ các thông số thiết kế, cấu hình wave, thời gian và hình ảnh minh họa bố cục màn chơi cho 15 level đầu tiên của Galaxy Strike:" "Times New Roman" 11 $false $false 0 6

    $tblLvDetail = $doc.Tables.Add($selection.Range, 16, 13)
    $tblLvDetail.Borders.Enable = $true
    FormatHeaderRow $tblLvDetail 13 0x203864
    $tblLvDetail.Rows.Item(1).Range.Font.Color = 0xFFFFFF
    $tblLvDetail.Rows.Item(1).Range.Font.Size = 7.5

    # Column widths
    $tblLvDetail.Columns.Item(1).Width = 24  # Lvl
    $tblLvDetail.Columns.Item(2).Width = 36  # Diff
    $tblLvDetail.Columns.Item(3).Width = 42  # Note
    $tblLvDetail.Columns.Item(4).Width = 45  # Mech
    $tblLvDetail.Columns.Item(5).Width = 26  # TSet
    $tblLvDetail.Columns.Item(6).Width = 26  # TSol
    $tblLvDetail.Columns.Item(7).Width = 28  # Rew
    $tblLvDetail.Columns.Item(8).Width = 30  # Grid
    $tblLvDetail.Columns.Item(9).Width = 24  # Qty
    $tblLvDetail.Columns.Item(10).Width = 44 # Zone
    $tblLvDetail.Columns.Item(11).Width = 48 # Enemies
    $tblLvDetail.Columns.Item(12).Width = 35 # Bullet
    $tblLvDetail.Columns.Item(13).Width = 72 # Image

    $lvdHeaders = @("Lvl", "Diff", "Note", "Mech", "TimeSet", "TimeSol", "Coin", "Grid", "Qty", "Zone", "Enemies", "Bullet", "Hình ảnh")
    for ($c = 0; $c -lt 13; $c++) { $tblLvDetail.Cell(1, $c+1).Range.Text = $lvdHeaders[$c] }

    $lvDetails = @(
        @{ lvl="1"; diff="Easy"; note="Làm quen"; mech="Formation"; tset="60"; tsol="20"; coin="32"; grid="2x2"; qty="4"; zone="Earth Orbit"; enemies="Scout"; bullet="Cấp 1"; img="level_01_preview.png" },
        @{ lvl="2"; diff="Easy"; note="Nhặt đạn"; mech="Không"; tset="75"; tsol="25"; coin="34"; grid="2x3"; qty="6"; zone="Earth Orbit"; enemies="Scout, Fig"; bullet="Cấp 1-2"; img="level_02_preview.png" },
        @{ lvl="3"; diff="Easy"; note="2 tia"; mech="Không"; tset="90"; tsol="30"; coin="36"; grid="2x4"; qty="8"; zone="Earth Orbit"; enemies="Fighter"; bullet="Cấp 2"; img="level_03_preview.png" },
        @{ lvl="4"; diff="Easy"; note="–"; mech="Không"; tset="105"; tsol="35"; coin="38"; grid="3x4"; qty="12"; zone="Earth Orbit"; enemies="Striker"; bullet="Cấp 2"; img="level_04_preview.png" },
        @{ lvl="5"; diff="Hard"; note="Mở Overdrive"; mech="Dive Attack"; tset="120"; tsol="60"; coin="105"; grid="3w"; qty="16"; zone="Earth Orbit"; enemies="Fighter, MB"; bullet="Cấp 3"; img="level_05_preview.png" },
        @{ lvl="6"; diff="Easy"; note="–"; mech="Không"; tset="115"; tsol="55"; coin="42"; grid="3x3"; qty="14"; zone="Earth Orbit"; enemies="Striker"; bullet="Cấp 3"; img="level_06_preview.png" },
        @{ lvl="7"; diff="Easy"; note="Cảm tử"; mech="Dive"; tset="125"; tsol="60"; coin="44"; grid="3x4"; qty="16"; zone="Earth Orbit"; enemies="Gunship"; bullet="Cấp 3"; img="level_07_preview.png" },
        @{ lvl="8"; diff="Easy"; note="–"; mech="Không"; tset="130"; tsol="65"; coin="46"; grid="3x4"; qty="18"; zone="Earth Orbit"; enemies="Gunship"; bullet="Cấp 3"; img="level_08_preview.png" },
        @{ lvl="9"; diff="Easy"; note="Giáp dày"; mech="Không"; tset="135"; tsol="70"; coin="48"; grid="3x4"; qty="18"; zone="Earth Orbit"; enemies="Cruiser"; bullet="Cấp 3-4"; img="level_09_preview.png" },
        @{ lvl="10"; diff="S-Hard"; note="Boss Zone 1"; mech="Mega Boss"; tset="160"; tsol="95"; coin="250"; grid="Boss"; qty="21"; zone="Earth Boss"; enemies="Vang, Boss"; bullet="Cấp 4"; img="level_10_preview.png" },
        @{ lvl="11"; diff="Easy"; note="Zone 2"; mech="Không"; tset="130"; tsol="65"; coin="52"; grid="3x4"; qty="16"; zone="Asteroid"; enemies="Striker"; bullet="Cấp 3-4"; img="level_11_preview.png" },
        @{ lvl="12"; diff="Easy"; note="Mở Shield"; mech="Hostile Fire"; tset="135"; tsol="65"; coin="54"; grid="3x4"; qty="18"; zone="Asteroid"; enemies="Gunship"; bullet="Cấp 4"; img="level_12_preview.png" },
        @{ lvl="13"; diff="Easy"; note="–"; mech="Không"; tset="140"; tsol="70"; coin="56"; grid="3x4"; qty="20"; zone="Asteroid"; enemies="Cruiser"; bullet="Cấp 4"; img="level_13_preview.png" },
        @{ lvl="14"; diff="Hard"; note="x2 coin"; mech="Không"; tset="145"; tsol="110"; coin="116"; grid="3w"; qty="22"; zone="Asteroid"; enemies="Destroyer"; bullet="Cấp 4-5"; img="level_14_preview.png" },
        @{ lvl="15"; diff="Easy"; note="Mini-Boss 2"; mech="Armor Defl"; tset="150"; tsol="75"; coin="155"; grid="Cmd"; qty="16"; zone="Asteroid"; enemies="Elite, MB"; bullet="Cấp 4-5"; img="level_15_preview.png" }
    )

    for ($r = 0; $r -lt 15; $r++) {
        $rowIdx = $r + 2
        $l = $lvDetails[$r]
        
        $tblLvDetail.Cell($rowIdx, 1).Range.Text = $l.lvl
        $tblLvDetail.Cell($rowIdx, 1).Range.ParagraphFormat.Alignment = 1
        
        $tblLvDetail.Cell($rowIdx, 2).Range.Text = $l.diff
        $tblLvDetail.Cell($rowIdx, 3).Range.Text = $l.note
        $tblLvDetail.Cell($rowIdx, 4).Range.Text = $l.mech
        
        $tblLvDetail.Cell($rowIdx, 5).Range.Text = $l.tset
        $tblLvDetail.Cell($rowIdx, 5).Range.ParagraphFormat.Alignment = 1
        
        $tblLvDetail.Cell($rowIdx, 6).Range.Text = $l.tsol
        $tblLvDetail.Cell($rowIdx, 6).Range.ParagraphFormat.Alignment = 1
        
        $tblLvDetail.Cell($rowIdx, 7).Range.Text = $l.coin
        $tblLvDetail.Cell($rowIdx, 7).Range.ParagraphFormat.Alignment = 1
        
        $tblLvDetail.Cell($rowIdx, 8).Range.Text = $l.grid
        $tblLvDetail.Cell($rowIdx, 9).Range.Text = $l.qty
        $tblLvDetail.Cell($rowIdx, 9).Range.ParagraphFormat.Alignment = 1
        
        $tblLvDetail.Cell($rowIdx, 10).Range.Text = $l.zone
        $tblLvDetail.Cell($rowIdx, 11).Range.Text = $l.enemies
        $tblLvDetail.Cell($rowIdx, 12).Range.Text = $l.bullet

        for ($c = 1; $c -le 12; $c++) {
            $tblLvDetail.Cell($rowIdx, $c).Range.Font.Name = "Times New Roman"
            $tblLvDetail.Cell($rowIdx, $c).Range.Font.Size = 7.5
        }

        # Chèn ảnh preview vào ô cột 13
        $cellImg = $tblLvDetail.Cell($rowIdx, 13)
        $cellImg.Range.ParagraphFormat.Alignment = 1
        $imgFullPath = Join-Path $assetsDir $l.img
        if (Test-Path $imgFullPath) {
            $sh = $cellImg.Range.InlineShapes.AddPicture($imgFullPath)
            $sh.Width = 52
            $sh.Height = 65
        }
    }

    $selection.Start = $tblLvDetail.Range.End
    $selection.TypeParagraph()

    # --- BẢNG THỜI GIAN, ĐỘ KHÓ CẢM NHẬN & REWARD ---
    AddHeading2 "2. Thời gian, độ khó cảm nhận và Reward"
    AddPara "Bảng dưới bổ sung các thông số cho từng level: thời gian đặt (Time Set), thời gian giải kỳ vọng của người chơi trung bình (Time Solve), cảm nhận độ khó và coin thưởng:" "Times New Roman" 11 $false $false 0 6

    $tblFeel = $doc.Tables.Add($selection.Range, 16, 8)
    $tblFeel.Borders.Enable = $true
    FormatHeaderRow $tblFeel 8 0x203864
    $tblFeel.Rows.Item(1).Range.Font.Color = 0xFFFFFF
    $tblFeel.Columns.Item(1).Width = 35
    $tblFeel.Columns.Item(2).Width = 50
    $tblFeel.Columns.Item(3).Width = 50
    $tblFeel.Columns.Item(4).Width = 45
    $tblFeel.Columns.Item(5).Width = 50
    $tblFeel.Columns.Item(6).Width = 50
    $tblFeel.Columns.Item(7).Width = 150
    $tblFeel.Columns.Item(8).Width = 50

    $fHeaders = @("Level", "Difficulty", "Grid", "Số Quái", "Time Set (s)", "Time Solve (s)", "Feeling độ khó", "Reward (coin)")
    for ($c = 0; $c -lt 8; $c++) { $tblFeel.Cell(1, $c+1).Range.Text = $fHeaders[$c] }

    $feelData = @(
        @("1", "Easy", "2x2", "4", "60", "20", "Rất dễ – làm quen thao tác kéo tàu", "32"),
        @("2", "Easy", "2x3", "6", "75", "25", "Rất dễ – làm quen nhặt đạn", "34"),
        @("3", "Easy", "2x4", "8", "90", "30", "Dễ – hỏa lực 2 tia thỏa mãn", "36"),
        @("4", "Easy", "3x4", "12", "105", "35", "Dễ – mật độ tăng nhẹ", "38"),
        @("5", "Hard", "3w", "16", "120", "60", "Khó vừa – Mini-Boss #1 & đòn bổ nhào", "105"),
        @("6", "Easy", "3x3", "14", "115", "55", "Dễ – thư giãn với 3 tia", "42"),
        @("7", "Easy", "3x4", "16", "125", "60", "Dễ – làm quen Kamikaze Dive", "44"),
        @("8", "Easy", "3x4", "18", "130", "65", "Dễ – vừa", "46"),
        @("9", "Easy", "3x4", "18", "135", "70", "Dễ – vừa", "48"),
        @("10", "Super Hard", "Boss", "21", "160", "95", "Rất khó – Flagship Boss Zone 1", "250"),
        @("11", "Easy", "3x4", "16", "130", "65", "Dễ – vừa", "52"),
        @("12", "Easy", "3x4", "18", "135", "65", "Dễ – mở Energy Shield", "54"),
        @("13", "Easy", "3x4", "20", "140", "70", "Dễ – vừa", "56"),
        @("14", "Hard", "3w", "22", "145", "110", "Khó – cần tính trước đường né đạn", "116"),
        @("15", "Easy", "Cmd", "16", "150", "75", "Dễ – làm quen Heavy Armor", "155")
    )

    for ($r = 0; $r -lt 15; $r++) {
        $rowIdx = $r + 2
        $row = $feelData[$r]
        for ($c = 0; $c -lt 8; $c++) {
            $cell = $tblFeel.Cell($rowIdx, $c+1)
            $cell.Range.Text = $row[$c]
            $cell.Range.Font.Name = "Times New Roman"
            $cell.Range.Font.Size = 9
            if ($c -eq 0 -or $c -eq 3 -or $c -eq 4 -or $c -eq 5 -or $c -eq 7) {
                $cell.Range.ParagraphFormat.Alignment = 1
            }
        }
    }

    $selection.Start = $tblFeel.Range.End
    $selection.TypeParagraph()

    # --- CÁCH TÍNH TIME SET & REWARD ---
    AddHeading2 "3. Cách tính Time Set & Reward"
    AddHeading3 "Time Set (Thời gian màn chơi)"
    AddPara "Thời gian hoàn thành được thiết lập theo độ khó và quy mô đội hình của màn chơi:" "Times New Roman" 10.5 $false $false 0 2
    AddPara "• 4 màn đầu: khoảng 60–105 giây/màn, giúp người chơi làm quen với gameplay." "Times New Roman" 10.5 $false $false 0 2
    AddPara "• Easy: khoảng 115–140 giây/màn." "Times New Roman" 10.5 $false $false 0 2
    AddPara "• Medium: khoảng 130–150 giây/màn." "Times New Roman" 10.5 $false $false 0 2
    AddPara "• Hard: khoảng 120–145 giây/màn." "Times New Roman" 10.5 $false $false 0 2
    AddPara "• Super Hard: khoảng 150–180 giây/màn." "Times New Roman" 10.5 $false $false 0 2
    AddPara "Thời gian được điều chỉnh dựa trên số lượng máu tổng của hạm đội (totalStageHP) và số lượng wave, nhằm đảm bảo người chơi có đủ thời gian giải quyết nhưng vẫn tạo áp lực." "Times New Roman" 10.5 $false $true 0 6

    AddHeading3 "Reward (Phần thưởng Coin)"
    AddPara "• Màn chơi thường (Standard): Coin = 30 + (Level x 2)" "Times New Roman" 10.5 $false $false 0 2
    AddPara "• Màn chơi Mini-Boss (mốc chia hết cho 5): Coin = 80 + (Level x 5)" "Times New Roman" 10.5 $false $false 0 2
    AddPara "• Màn chơi Major Boss (mốc chia hết cho 10): Coin = 150 + (Level x 10)" "Times New Roman" 10.5 $false $false 0 2
    AddPara "• Mốc đặc biệt Level 14: 116 Coin (x2 phần thưởng)" "Times New Roman" 10.5 $false $false 0 2
    AddPara "• Có thể xem Rewarded Ad để nhân đôi phần thưởng (x2 Coin)." "Times New Roman" 10.5 $false $false 0 6

    AddHeading3 "Revive (Hồi sinh)"
    AddPara "• Lần đầu: 900 Coin (hoặc xem 1 Rewarded Video miễn phí)" "Times New Roman" 10.5 $false $false 0 2
    AddPara "• Mỗi lần tiếp theo trong cùng level: +500 Coin (1400 Coin, 1900 Coin...)" "Times New Roman" 10.5 $false $false 0 2
    AddPara "• Khi hồi sinh: Tặng màn khiên bất tử trong 3 giây và hồi phục toàn bộ 3 máu." "Times New Roman" 10.5 $false $false 0 12

    AddPara "Các Sheet đính kèm: Level Documentation - Google Trang tính" "Times New Roman" 10.5 $true $true 0 0 0x803000

    # Save document
    Write-Host "Saving document to $outputDocx..."
    $doc.SaveAs2($outputDocx, 16) # 16 = wdFormatXMLDocument (.docx)
    $doc.Close()
    Write-Host "Done! Successfully exported Word document to $outputDocx"

} finally {
    $word.Quit()
    [System.Runtime.InteropServices.Marshal]::ReleaseComObject($word) | Out-Null
    [System.GC]::Collect()
    [System.GC]::WaitForPendingFinalizers()
}
