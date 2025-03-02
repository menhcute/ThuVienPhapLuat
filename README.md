Đây là chương trình Crawl dữ liệu như Văn bản pháp luật, Công văn... từ trang web Thư viện pháp luật (https://thuvienphapluat.vn/).
Sau khi Crawl dữ liệu xong, chương trình sẽ lưu dữ liệu vào database, chương trình này hiện hỗ trợ lưu vào Sql Server.

How to use:
1. Mở chương trình này trong Visual Studio 2022.
1. Tìm hàm public static void SaveToDatabase().
2. Ở biến String connectionString, thay đổi tên Server; Database cho giống cấu hình của máy mình.
3. Sau đó Clean, Build chương trình
4. Sau đó Run chương trình và chương trình sẽ Crawl dữ liệu và lưu vào Database

Query để tạo Database trong Sql Server:
CREATE DATABASE LawDatabase;
GO

USE LawDatabase;
GO

CREATE TABLE Laws (
    ID INT IDENTITY(1,1) PRIMARY KEY,
    Text NVARCHAR(MAX) NOT NULL,
    Organization NVARCHAR(MAX) NOT NULL,
    LawName NVARCHAR(MAX) NOT NULL,
    LawNo NVARCHAR(50) NOT NULL,
    DateIssue DATE NOT NULL,
    Signee NVARCHAR(255) NOT NULL
);
