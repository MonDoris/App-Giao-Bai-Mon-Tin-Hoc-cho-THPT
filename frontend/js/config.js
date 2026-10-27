// Cấu hình địa chỉ API (QD-88, AC-484). Đây là tệp DUY NHẤT cần sửa khi đổi môi trường (không cần build).
// - Sửa khi triển khai: ví dụ 'https://api.example.com'.
// - Chuỗi rỗng '' nghĩa là cùng origin với trang này (khi reverse proxy chuyển /api về backend).
// - Không có dấu '/' ở cuối; giá trị phải là origin của backend (scheme + tên miền + cổng, không có đường dẫn).
// - Origin của trang giao diện phải nằm trong Cors:AllowedOrigins của backend.
export const API_BASE = 'http://localhost:5000';
