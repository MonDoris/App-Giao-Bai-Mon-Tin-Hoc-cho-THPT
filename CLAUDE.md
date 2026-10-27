# Autonomous SDLC Team Workflow

Dự án này vận hành theo mô hình Đội ngũ AI tự động khép kín với Người dùng đóng vai trò Khách hàng (Product Owner).

## 1. Cấu trúc nhân sự & Phân vai
- **Khách hàng (User):** Đưa ra yêu cầu bằng ngôn ngữ tự nhiên.
- **BA (`@business-analyst` - Sonnet):** Chuyển yêu cầu thô thành file đặc tả kỹ thuật với mã nghiệm thu [AC-xx].
- **Dev (`@dev-engineer` - Sonnet):** Viết mã nguồn, tích hợp và chạy build.
- **QA (`@qa-tester` - Haiku):** Kiểm thử độc lập dựa trên mã [AC-xx], xuất bảng Pass/Fail.

## 2. Quy trình Vòng lặp Khép kín (The Closed Loop)
Khi Khách hàng yêu cầu một tính năng hoặc chỉnh sửa:
1. **Bước 1 (BA):** Kích hoạt `@business-analyst`. BA phân tích và tạo file `docs/specs/<tên-tính-năng>.md`.
2. **Bước 2 (Dev):** Kích hoạt `@dev-engineer`. Dev đọc file đặc tả, code toàn bộ các file liên quan và tự chạy lệnh build/compile để xác nhận không có lỗi cú pháp.
3. **Bước 3 (QA):** Kích hoạt `@qa-tester`. QA đối chiếu mã nguồn với danh sách [AC-xx] và xuất Bảng Báo cáo Nghiệm thu.
4. **Bước 4 (Phản hồi & Sửa lỗi):**
   - **Nếu có bất kỳ tiêu chí [FAIL]:** Tự động chuyển báo cáo lỗi cho `@dev-engineer` sửa lại, sau đó `@qa-tester` kiểm tra lại.
   - **Giới hạn lặp:** Tối đa 3 vòng lặp (Max 3 retries). Nếu sau 3 lần vẫn FAIL, dừng lại và xin ý kiến Khách hàng.
   - **Nếu 100% [PASS]:** Bàn giao kết quả hoàn chỉnh và hiển thị bảng nghiệm thu cho Khách hàng.

## 3. Tiêu chuẩn Kỹ thuật chung
- Mã nguồn phải giữ tính nhất quán, bám sát kiến trúc sẵn có của repository.
- Mọi logic nghiệp vụ phát sinh đều phải được định nghĩa trong tài liệu đặc tả trước khi code.
