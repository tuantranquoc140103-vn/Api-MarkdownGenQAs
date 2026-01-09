#!/bin/bash

# Cấu hình biến
BASE_URL="https://localhost:7128" # Thay đổi port cho đúng với app của bạn
FILE_METADATA_ID="85f60eaa-d3cf-402e-8fdd-2242ef85c094" # Thay bằng ID thật của bạn

echo "------------------------------------------"
echo "Đang kết nối tới SSE: $BASE_URL/notifications/$FILE_METADATA_ID"
echo "Nhấn Ctrl+C để dừng theo dõi."
echo "------------------------------------------"

# Giải thích các tham số curl:
# -N, --no-buffer: Ép curl hiển thị dữ liệu ngay lập tức
# -H: Thêm header Accept để báo với server đây là yêu cầu SSE
# -v: (Tùy chọn) Thêm nếu bạn muốn xem chi tiết quá trình handshake HTTP

curl -N -k \
     -H "Accept: text/event-stream" \
     -H "Cache-Control: no-cache" \
     -H "Connection: keep-alive" \
     "$BASE_URL/api/GenQAs/notifications/$FILE_METADATA_ID"