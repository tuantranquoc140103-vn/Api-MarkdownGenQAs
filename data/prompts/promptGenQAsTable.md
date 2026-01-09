You are an expert Business Requirement Document analyst.

You will receive:
1) a document-level summary
2) one chunk that is a TABLE (HTML) with title and hierarchy metadata

Your task is to generate Question–Answer pairs ONLY from the TABLE content. Stay consistent with the overall document purpose from the summary. Do NOT hallucinate information that is not present in the table.

================ CATEGORY =================
Each QA must be assigned ONE category from the list below:

objective, scope, definition, rule, constraint, process, data,
exception, change_history, stakeholder, approval, version, other

Pick the most appropriate category based on business meaning.

================ CRITICAL RULE: EMPTY TABLE =================
If the table has NO DATA ROWS (e.g., empty <tbody>, only headers, blank cells):
- return EXACTLY ONE QA item only
- DO NOT apply any coverage rules
- DO NOT apply multi-level QA rules
- DO NOT infer or fabricate content
- DO NOT mention that the table is empty
The single QA should only capture the intended purpose of the table from its title and/or column headers.

================ WHEN TABLE HAS DATA =================
(Apply the rules below ONLY if there are data rows)

1) Generate QAs at 3 abstraction levels:
- High level: overall meaning and purpose of the table
- Mid level: by sections, groups, or column meanings
- Detail level: per important or distinctive rows

2) Ensure coverage:
- major columns
- major row groups or sections
- exceptions such as N/A if present
- process flows if present

Minimum QA count by size:
- >20 rows → at least 20 QAs
- >50 rows → at least 30 QAs
- >100 rows → at least 30 QAs

================ TABLE UNDERSTANDING =================
The table may include HTML (<table>, <tr>, <td>, <th>, <br>, etc.). You must:
- normalize text inside HTML
- interpret rows and columns as structured data
- respect numbering or grouping in the table

Do NOT hallucinate missing content.

================ CATEGORY RULES ================
Each QA must be labeled with exactly ONE of the following categories:

objective        → mục tiêu, mục đích chính của bảng hoặc nội dung được trình bày
scope            → phạm vi áp dụng hoặc giới hạn của nội dung trong bảng
definition       → định nghĩa thuật ngữ, viết tắt, khái niệm xuất hiện trong bảng
rule             → quy tắc nghiệp vụ hoặc quy định bắt buộc phải tuân thủ
constraint       → ràng buộc pháp lý, kỹ thuật, hệ thống hoặc nghiệp vụ
process          → quy trình, luồng xử lý hoặc các bước thực hiện
data             → thông tin dữ liệu, trường dữ liệu, danh mục, danh sách
exception        → trường hợp ngoại lệ, không áp dụng, xử lý lỗi hoặc N/A
change_history   → lịch sử thay đổi, cập nhật, chỉnh sửa nội dung hoặc phiên bản
stakeholder      → các bên liên quan, vai trò, trách nhiệm hoặc đơn vị phụ trách
approval         → phê duyệt, ký duyệt, xác nhận trách nhiệm
version          → phiên bản của tài liệu, ngày hiệu lực, ký hiệu version
reference        → tài liệu tham khảo, văn bản viện dẫn, tiêu chuẩn hoặc tài liệu liên quan được sử dụng làm căn cứ xây dựng tài liệu hiện tại
other            → nội dung quan trọng khác không thuộc các nhóm trên

================ OUTPUT =================
Write in Vietnamese.
Return valid JSON ONLY.

Each QA must include:
- category
- question
- answer

Avoid copying the table verbatim; synthesize meaning.
Ensure all double quotes inside string values are escaped as \". Use only standard JSON-escaped characters. Do not include raw newlines within strings; use \n instead

================ INPUTS =================
Document Name: {0}
JSON Schema: {1}
Document Summary: {2}
Table Title: {3}
Title Hierarchy: {4}

# TABLE CONTENT
{5}
