# Role
Bạn là một Senior Frontend Developer chuyên về Prototyping cho các hệ thống AI.

# Task
Tạo một trang HTML duy nhất (SPA) để hiển thị dữ liệu RAG với giao diện hiện đại, sạch sẽ.

# Tech Stack (CDN)
- **Tailwind CSS**: Styling giao diện.
- **Alpine.js**: Quản lý State (chuyển tab, chọn chunk, hiển thị dữ liệu).
- **Marked.js**: Render Markdown nội dung chunk.

# Data Schema (Dựa trên mẫu JSON cung cấp)
Dữ liệu trong `pathChunkQAs` sẽ có cấu trúc như sau:
{
    "chunk_infor": {
        "type": "allContent", or table
        "tokens_count": 1646,
        "title": "...",
        "content": "..." // Lưu ý: content có thể chứa Markdown và các thẻ <table> HTML thô
    },
    "qas": [
        { "category": "...", "question": "...", "answer": "..." }
    ]
}

# Input Variables (Khai báo ở đầu <script>)
- `pathDataSource`: Markdown của toàn bộ tài liệu.
- `pathChunkQAs`: JSON list các Chunk và QAs liên quan.
- `pathSummaryDocument`: Văn bản tóm tắt tài liệu.
- `pathChunkQAsSummary`: JSON list các QAs của phần Summary.

# Yêu cầu Giao diện (3 Tabs)

## Tab 1: Văn bản OCR
- Hiển thị nội dung Markdown toàn bộ từ `pathDataSource`.

## Tab 2: Tóm tắt (Summary)
- Hiển thị văn bản tóm tắt phía trên và danh sách QAs của summary phía dưới.

## Tab 3: Chi tiết Chunks (Master-Detail Layout)
Đây là phần quan trọng nhất. Hãy thiết kế như sau:

1. **Trạng thái mặc định**:
   - Hiển thị danh sách tất cả các chunk dưới dạng list (Card nhỏ).
   - Mỗi card chỉ hiển thị tiêu đề và nội dung preview tối đa **2 dòng (Sử dụng CSS line-clamp-2/ellipsis)**.

2. **Khi Click vào một Chunk (Split Screen)**:
   - Chia màn hình Tab 3 thành 2 cột (Left: 1/3, Right: 2/3).
   - **Cột bên trái (List)**: Giữ nguyên danh sách các chunk để người dùng có thể chuyển đổi nhanh. Highlight chunk đang được chọn.
   - **Cột bên phải (Detail)**: Chia tiếp làm 2 phần theo chiều dọc (Top-Bottom):
     - **Phần trên (Content)**: Hiển thị nội dung đầy đủ của chunk (Render Markdown/Table) kèm Metadata (Title, Tokens, Type badge: "Table Chunk" hoặc "Text Chunk").
     - **Phần dưới (QAs)**: Hiển thị danh sách các câu hỏi và câu trả lời liên quan riêng đến chunk đó.

# Yêu cầu bổ sung
- **CSS cho Table**: Đảm bảo các bảng trong nội dung chunk có border, padding và header rõ ràng (Tailwind mặc định xóa style này, cần viết thêm CSS thủ công cho thẻ table).
- **Scroll Behavior**: Cột bên trái và cột bên phải (phần detail) phải có khả năng cuộn độc lập (overflow-y-auto) để không ảnh hưởng đến giao diện chung.
- **Tính thẩm mỹ**: Sử dụng màu sắc nhẹ nhàng, bo góc (rounded-lg), đổ bóng (shadow) cho các card để tạo cảm giác chuyên nghiệp.

# Output
Trả về một file .html duy nhất đầy đủ CSS/JS.