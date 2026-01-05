You are an expert Business Requirement Document analyst.

Your task is to read:
1) The document-level summary provided below
2) One specific document chunk that is a TABLE extracted from the document, including title and title hierarchy metadata

Your job is to generate high-quality Question and Answer pairs ONLY from the TABLE content in this chunk, while staying aligned with the overall purpose of the document based on the summary.

Do not invent or hallucinate information that is not present in the table.

================ CATEGORY RULES ================
Each QA must be labeled with one of the following categories:

objective        → mục tiêu, ý nghĩa bảng
scope            → phạm vi nội dung được liệt kê
definition       → thuật ngữ, viết tắt, định nghĩa
rule             → quy tắc, điều kiện áp dụng
constraint       → hạn chế, ràng buộc
process          → bước quy trình hoặc luồng xử lý
data             → bảng dữ liệu, danh mục, danh sách
exception        → trường hợp ngoại lệ, N/A, không áp dụng
change_history   → lịch sử thay đổi, chỉnh sửa, cập nhật
stakeholder      → vai trò, đối tượng liên quan
approval         → phê duyệt, ký duyệt, trách nhiệm
version          → phiên bản, hiệu lực tài liệu
other            → bảng không thuộc các loại trên

Choose the most appropriate category based on the business meaning of the table.

================ HOW TO INTERPRET THE TABLE ================
The chunk may contain HTML such as <table>, <tr>, <td>, <th>, <br>, etc.

You must:
- treat HTML tables as structured business data
- normalize text inside HTML tags before analysis
- infer meaning from rows and columns
- understand list or hierarchy conveyed by numbering

If the table represents:
- table of contents → reflect structure and index meaning
- change logs / versioning → map to change_history / version
- approval or signatures → map to approval / stakeholder
- process steps → map to process
- report lists → map to data or scope
- glossary → map to definition

If the table contains placeholders like “N/A”:
- explicitly treat them as exception

If information is incomplete:
- clearly state that completeness cannot be determined from the table
- DO NOT guess or fabricate details

================ GENERAL INSTRUCTIONS ================
- Use the document summary only as high-level context
- Work strictly within this table’s content
- Prefer business meaning over visual formatting
- Do not copy entire table rows verbatim — synthesize meaning
- If the table only provides structure without details, explain its purpose

================ OUTPUT REQUIREMENTS ================
Write in Vietnamese.

Each item in the list must include:
- category
- question
- answer

Write questions clearly and answers concise but informative.
Write in Vietnamese.

================ DOCUMENT NAME ================
example2

================ JSON SCHEMA ================
{
  "type": "array",
  "items": {
    "type": "object",
    "properties": {
      "category": {
        "description": "Thể loại câu hỏi",
        "enum": [
          "objective",
          "scope",
          "definition",
          "rule",
          "constraint",
          "process",
          "data",
          "exception",
          "change_History",
          "stakeholder",
          "approval",
          "version",
          "other"
        ]
      },
      "question": {
        "description": "Câu hỏi tóm tắt mang tính khái quát cao",
        "type": "string"
      },
      "answer": {
        "description": "Câu trả lời chi tiết nhưng súc tích, trích xuất từ tài liệu",
        "type": "string"
      }
    },
    "required": [
      "question",
      "answer"
    ]
  }
}

================ DOCUMENT SUMMARY ================
## Tài liệu **example2** nói về điều gì?
Tài liệu này là một Tài liệu Đặc tả Yêu cầu Nghiệp vụ (Business Requirement Document - BRD) của Ngân hàng SeABank, chuyên về quy trình thu thập dữ liệu sinh trắc học của khách hàng. Tài liệu này mô tả chi tiết các yêu cầu, quy trình và quy định liên quan đến việc thu thập, xác thực và xử lý dữ liệu sinh trắc học để phục vụ cho việc xác thực giao dịch tài chính online và các nghiệp vụ khác của ngân hàng. Tài liệu này được biên soạn cho ứng dụng SeAMobile và được sử dụng để hướng dẫn các thành viên dự án Chuyển đổi số trong việc thu thập và xử lý dữ liệu sinh trắc học của khách hàng.

## Loại tài liệu
Tài liệu Đặc tả Yêu cầu Nghiệp vụ (Business Requirement Document - BRD)

## Mục tiêu chính
Tài liệu này có mục đích chính là mô tả yêu cầu thu thập dữ liệu sinh trắc học của khách hàng để phục vụ cho việc xác thực các giao dịch tài chính trên kênh online và các nghiệp vụ khác của ngân hàng. Tài liệu này cũng quy định các bước cụ thể trong quy trình thu thập, xác thực và xử lý dữ liệu sinh trắc học, đảm bảo tuân thủ pháp luật và an ninh thông tin.

## Đối tượng độc giả chính
- Các thành viên dự án Chuyển đổi số
- Nhân viên ngân hàng SeABank liên quan đến quy trình thu thập dữ liệu sinh trắc học
- Các bên liên quan đến hệ thống phần mềm và nghiệp vụ ngân hàng số

## Các chủ đề chính
- Thu thập dữ liệu sinh trắc học
- Xác thực giao dịch tài chính online
- Quy trình nghiệp vụ thu thập sinh trắc học
- Dữ liệu sinh trắc học hợp lệ
- Kiểm tra và xác thực dữ liệu sinh trắc học
- Hậu kiểm và giám sát dữ liệu sinh trắc học
- Mã trạng thái giám sát
- Lỗi và xử lý lỗi trong quá trình thu thập sinh trắc học

## Vấn đề hoặc nhu cầu mà tài liệu hướng tới giải quyết
- SeABank chưa có chức năng thu thập dữ liệu sinh trắc học cho khách hàng.
- Dữ liệu sinh trắc học thu thập hợp lệ cần được gửi qua Bộ Công An để kiểm tra xác thực thông tin.
- Thu thập và xử lý dữ liệu sinh trắc học một cách an toàn và tuân thủ pháp luật.

================ TABLE CHUNK TO ANALYZE ================

Title: ## A – Hiệu lực của Tài liệu
Title Hirarchy: # BẢNG THEO DÕI HIỆU LỰC VÀ THAY ĐỔI

# Table 

<table><thead><tr><td>Đóng dấu</td><td>Ngày đóng dấu</td><td>Người đóng dấu</td></tr></thead><tbody><tr><td></td><td></td><td></td></tr><tr><td></td><td></td><td></td></tr></tbody></table>
