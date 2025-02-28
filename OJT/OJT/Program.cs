using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Data.SqlClient;

class Program
{
    static void Main()
    {
        Console.OutputEncoding = Encoding.UTF8; // Hỗ trợ hiển thị tiếng Việt trong console

        ChromeOptions options = new ChromeOptions();
        options.AddArgument("--start-maximized");
        IWebDriver driver = new ChromeDriver(options);

        driver.Navigate().GoToUrl("https://thuvienphapluat.vn/page/tim-van-ban.aspx?keyword=&type=3&match=True&area=0");
        Thread.Sleep(3000);

        HashSet<string> uniqueHrefs = new HashSet<string>();
        int count = 0;

        while (true)
        {
            var contentDivs = driver.FindElements(By.CssSelector("div[class^='content-']"));

            foreach (var div in contentDivs)
            {
                try
                {
                    var linkElement = div.FindElement(By.CssSelector("a[onclick='Doc_CT(MemberGA)']"));
                    string href = linkElement.GetAttribute("href");
                    string fileName = linkElement.Text.Trim().Replace("/", "_").Replace("\\", "_").Replace(":", "_");

                    if (!string.IsNullOrEmpty(href) && uniqueHrefs.Add(href))
                    {
                        IJavaScriptExecutor js = (IJavaScriptExecutor)driver;
                        js.ExecuteScript("window.open(arguments[0]);", href);
                        Thread.Sleep(100);

                        var tabs = driver.WindowHandles;
                        driver.SwitchTo().Window(tabs.Last());

                        var contentDiv = driver.FindElement(By.CssSelector(".content1"));
                        List<string> contentTexts = new List<string> { "Href: " + href };

                        var elements = contentDiv.FindElements(By.XPath(".//*"));
                        HashSet<string> paragraphsInTables = new HashSet<string>();

                        foreach (var element in elements)
                        {
                            if (element.TagName == "table")
                            {
                                var tableParagraphs = element.FindElements(By.XPath(".//p"));
                                foreach (var p in tableParagraphs)
                                {
                                    paragraphsInTables.Add(p.Text.Trim());
                                }
                            }
                        }

                        foreach (var element in elements)
                        {
                            if (element.TagName == "table")
                            {
                                string width = element.GetAttribute("width");
                                List<string> tableTexts = new List<string>();
                                var rows = element.FindElements(By.TagName("tr"));

                                bool shouldSkipTable = false;
                                HashSet<IWebElement> tdsToRemove = new HashSet<IWebElement>();

                                foreach (var row in rows)
                                {
                                    var cells = row.FindElements(By.TagName("td"));
                                    foreach (var cell in cells)
                                    {
                                        string cellText = cell.Text.Trim().ToLower();
                                        if (cellText.Contains("kính gửi"))
                                        {
                                            shouldSkipTable = true;
                                            break;
                                        }
                                        if (cellText.Contains("nơi nhận"))
                                        {
                                            tdsToRemove.Add(cell);
                                        }
                                    }
                                    if (shouldSkipTable) break;
                                }

                                if (!shouldSkipTable)
                                {
                                    foreach (var row in rows)
                                    {
                                        var cells = row.FindElements(By.TagName("td"));
                                        List<string> rowTexts = new List<string>();

                                        foreach (var cell in cells)
                                        {
                                            if (!tdsToRemove.Contains(cell))
                                            {
                                                rowTexts.Add(cell.Text.Trim());
                                            }
                                        }

                                        if (width == "100%" && rows.Count > 1)
                                        {
                                            tableTexts.Add(string.Join(" || ", rowTexts));
                                        }
                                        else
                                        {
                                            tableTexts.AddRange(rowTexts);
                                        }
                                    }
                                    contentTexts.AddRange(tableTexts);
                                }
                            }
                            else if (element.TagName == "p" && !paragraphsInTables.Contains(element.Text.Trim()))
                            {
                                contentTexts.Add(element.Text.Trim());
                            }
                        }

                        string content = ExtractMainContent(contentTexts); // Chỉ lấy phần nội dung chính  
                        string organization = ExtractOrganization(contentTexts);
                        string lawName = ExtractLawName(fileName);
                        string lawNo = ExtractLawNo(contentTexts);
                        string dateIssue = ExtractDateIssue(contentTexts);
                        string signee = ExtractSignee(contentTexts);

                        SaveToDatabase(content, organization, lawName, lawNo, dateIssue, signee);
                        Console.WriteLine($"Đã lưu vào Database: {lawName}");

                        Console.WriteLine("===== Danh sách dòng (đã loại khoảng trắng) =====");
                        foreach (var line in contentTexts.Select(line => line.Trim()).Reverse().ToList())
                        {
                            Console.WriteLine(line);
                        }
                        Console.WriteLine("=======================================");


                        driver.Close();
                        driver.SwitchTo().Window(tabs.First());
                        count++;
                    }
                }
                catch (NoSuchElementException)
                {
                    Console.WriteLine("Không tìm thấy liên kết phù hợp.");
                }
            }

            Console.WriteLine("Tổng số href tìm thấy: " + uniqueHrefs.Count);

            if (count % 20 == 0)
            {
                try
                {
                    var nextPageButton = driver.FindElements(By.CssSelector("a[rel='nofollow']"))
                        .FirstOrDefault(a => a.Text.Trim() == "Trang sau");

                    if (nextPageButton != null)
                    {
                        nextPageButton.Click();
                        Thread.Sleep(2000);
                    }
                    else
                    {
                        Console.WriteLine("Không tìm thấy nút chuyển trang.");
                        break;
                    }
                }
                catch (NoSuchElementException)
                {
                    Console.WriteLine("Không tìm thấy nút chuyển trang.");
                    break;
                }
            }
        }

        driver.Quit();
    }

    public static void SaveToDatabase(string text, string organization, string lawName, string lawNo, string dateIssue, string signee)
    {
        string connectionString = "Server=localhost;Database=LawDatabase;Trusted_Connection=True;";

        using (SqlConnection connection = new SqlConnection(connectionString))
        {
            connection.Open();

            string query = "INSERT INTO Laws (Text, Organization, LawName, LawNo, DateIssue, Signee) " +
                           "VALUES (@Text, @Organization, @LawName, @LawNo, @DateIssue, @Signee)";

            using (SqlCommand command = new SqlCommand(query, connection))
            {
                command.Parameters.AddWithValue("@Text", text);
                command.Parameters.AddWithValue("@Organization", organization);
                command.Parameters.AddWithValue("@LawName", lawName);
                command.Parameters.AddWithValue("@LawNo", lawNo);
                command.Parameters.AddWithValue("@DateIssue", DateTime.Parse(dateIssue));
                command.Parameters.AddWithValue("@Signee", signee);

                command.ExecuteNonQuery();
            }
        }
        Console.WriteLine("Đã lưu vào database!");
    }

    //Phần này là phần xử lí data được Crawl về (contentTexts) để nó ném vào các cột trong database

    // Lấy nội dung chính, bỏ phần tiêu đề
    public static string ExtractMainContent(List<string> contentTexts)
    {
        return string.Join("\n", contentTexts.Skip(5)); // Bỏ qua 5 dòng đầu nếu chứa metadata
    }

    // Tìm tổ chức ban hành
    public static string ExtractOrganization(List<string> contentTexts)
    {
        return contentTexts.FirstOrDefault(line =>
            line == line.ToUpper() && line.Length > 5 &&
            !line.Contains("CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM")) ?? "Không xác định";
    }

    // Tìm tiêu đề văn bản
    public static string ExtractLawName(string fileName)
    {
        return Path.GetFileNameWithoutExtension(fileName);
    }

    // Tìm số hiệu văn bản (Số: XXX/YYYY-ABC)
    public static string ExtractLawNo(List<string> contentTexts)
    {
        // Tìm dòng bắt đầu bằng "Số:" và không quá dài
        string lawNo = contentTexts.FirstOrDefault(line =>
            line.StartsWith("Số:") && line.Length < 30);

        if (!string.IsNullOrEmpty(lawNo)) return lawNo;

        // Nếu không có, tìm số hiệu trong văn bản
        var match = Regex.Match(string.Join(" ", contentTexts), @"Số:\s*([\w\/.-]+)");
        return match.Success ? match.Groups[1].Value : "Không xác định";
    }

    // Tìm ngày ban hành
    public static string ExtractDateIssue(List<string> contentTexts)
    {
        var match = Regex.Match(string.Join(" ", contentTexts), @"ngày (\d{1,2}) tháng (\d{1,2}) năm (\d{4})");
        return match.Success ? $"{match.Groups[3].Value}-{match.Groups[2].Value.PadLeft(2, '0')}-{match.Groups[1].Value.PadLeft(2, '0')}" : DateTime.Now.ToString("yyyy-MM-dd");
    }   

    // Tìm người ký
    public static string ExtractSignee(List<string> contentTexts)
    {
        //var lines = contentTexts.Select(line => line.Trim()).Reverse().ToList(); // Đọc từ dưới lên, bỏ khoảng trắng dư

        string signee = "Không xác định";
        //for (int i = 0; i < lines.Count - 1; i++) // Lặp qua các dòng
        //{
        //    string line = lines[i];

        //    // Nếu dòng chứa chức danh, kiểm tra luôn dòng tiếp theo
        //    if (Regex.IsMatch(line, @"(BỘ TRƯỞNG|KT\.|TL\.|CHỦ TỊCH|KÝ TÊN|THỨ TRƯỞNG|GIÁM ĐỐC)", RegexOptions.IgnoreCase))
        //    {
        //        string possibleName = lines[i + 1];

        //        // Kiểm tra xem dòng tiếp theo có thể là tên không (không chứa số hoặc ký tự đặc biệt)
        //        if (!Regex.IsMatch(possibleName, @"[\d:(){}]") && possibleName.Length > 3)
        //        {
        //            signee = possibleName;
        //            break;
        //        }
        //    }
        //}

        return signee;
    }

    public static string ConvertToFileName(string title)
    {
        Dictionary<string, string> vietnameseChars = new Dictionary<string, string>()
        {
            { "Đ", "D" }, { "đ", "d" },
            { "á", "a" }, { "à", "a" }, { "ả", "a" }, { "ã", "a" }, { "ạ", "a" },
            { "ă", "a" }, { "ắ", "a" }, { "ằ", "a" }, { "ẳ", "a" }, { "ẵ", "a" }, { "ặ", "a" },
            { "â", "a" }, { "ấ", "a" }, { "ầ", "a" }, { "ẩ", "a" }, { "ẫ", "a" }, { "ậ", "a" },
            { "é", "e" }, { "è", "e" }, { "ẻ", "e" }, { "ẽ", "e" }, { "ẹ", "e" },
            { "ê", "e" }, { "ế", "e" }, { "ề", "e" }, { "ể", "e" }, { "ễ", "e" }, { "ệ", "e" },
            { "í", "i" }, { "ì", "i" }, { "ỉ", "i" }, { "ĩ", "i" }, { "ị", "i" },
            { "ó", "o" }, { "ò", "o" }, { "ỏ", "o" }, { "õ", "o" }, { "ọ", "o" },
            { "ô", "o" }, { "ố", "o" }, { "ồ", "o" }, { "ổ", "o" }, { "ỗ", "o" }, { "ộ", "o" },
            { "ơ", "o" }, { "ớ", "o" }, { "ờ", "o" }, { "ở", "o" }, { "ỡ", "o" }, { "ợ", "o" },
            { "ú", "u" }, { "ù", "u" }, { "ủ", "u" }, { "ũ", "u" }, { "ụ", "u" },
            { "ư", "u" }, { "ứ", "u" }, { "ừ", "u" }, { "ử", "u" }, { "ữ", "u" }, { "ự", "u" },
            { "ý", "y" }, { "ỳ", "y" }, { "ỷ", "y" }, { "ỹ", "y" }, { "ỵ", "y" }
        };

        foreach (var item in vietnameseChars)
        {
            title = title.Replace(item.Key, item.Value);
        }

        title = Regex.Replace(title, @"[^\w\s-]", ""); // Xóa ký tự đặc biệt
        title = Regex.Replace(title, @"\s+", "_").Trim(); // Thay khoảng trắng bằng "_"

        if (title.Length > 200)
        {
            title = title.Substring(0, 200);
        }

        return $"{title}.txt";
    }
}