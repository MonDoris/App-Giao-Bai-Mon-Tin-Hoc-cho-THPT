// Văn bản mẫu cho nút "Điền mẫu" (QD-75). Chung chung, chỉ điền vào ô trên màn hình, không gọi API.
// Lưu ý: không đặt vào đây các tiêu đề bài mẫu bị cấm bởi ArchitectureTests.Sample_lesson_content_is_not_hard_coded_in_source.
// Thêm mẫu mới: thêm khóa vào SAMPLE hoặc EXAMPLE rồi gọi từ trình soạn.

export const SAMPLE = {
  lesson: { title: 'Vòng lặp for trong Python', description: 'Làm quen với vòng lặp for và hàm range để lặp lại một việc nhiều lần.' },
  summary: [
    'Vòng lặp for giúp lặp lại một nhóm lệnh nhiều lần mà không phải viết lại từng lệnh.',
    '- Hàm `range(n)` tạo dãy số từ 0 đến n - 1.',
    '- Các lệnh nằm trong vòng lặp phải thụt lề vào trong.',
    '```python\nfor i in range(3):\n    print(i)\n```',
  ],
  notes: [
    'Nhớ viết dấu hai chấm ở cuối dòng `for`.',
    'Các lệnh trong vòng lặp phải thụt lề bằng nhau.',
    'Hàm `range` dừng trước giá trị cuối, không lấy giá trị cuối.',
  ],
  conclusion: [
    'Sau bài này, em biết dùng vòng lặp for với range để lặp một việc nhiều lần.',
    '- Biết viết cú pháp for và range.',
    '- Biết thụt lề các lệnh trong vòng lặp.',
  ],
  exercise: {
    title: 'In bình phương các số từ 1 đến n',
    difficulty: 'Easy',
    statement: 'Cho số nguyên dương `n`. Hãy in ra bình phương của từng số từ 1 đến `n`, mỗi số trên một dòng.\n\n- Dùng vòng lặp `for` và hàm `range`.\n- Mỗi kết quả in trên một dòng riêng.',
    inputFormat: 'Một dòng chứa số nguyên dương n.',
    outputFormat: 'n dòng, dòng thứ i chứa bình phương của i.',
    constraints: '1 <= n <= 100',
    example: { input: '3', output: '1\n4\n9', explanation: 'Các số 1, 2, 3 có bình phương lần lượt là 1, 4, 9.' },
    starterCode: 'n = int(input())\n# Viết tiếp bên dưới\n',
    solutionCode: 'n = int(input())\nfor i in range(1, n + 1):\n    print(i * i)\n',
    solutionExplanation: 'Vòng lặp chạy i từ 1 đến n. Mỗi lần lặp in ra i nhân i.',
  },
};

// Ví dụ ngắn hiển thị trong hộp "Ví dụ" của từng bước (chỉ để minh họa, không tự điền).
export const EXAMPLE = {
  title: 'Vòng lặp for trong Python',
  description: 'Làm quen với vòng lặp for để lặp lại một việc nhiều lần.',
  summary: 'Vòng lặp for giúp lặp lại một việc nhiều lần. Hàm range tạo dãy số để lặp.',
  notes: 'Nhớ viết dấu hai chấm ở cuối dòng for.',
  conclusion: 'Sau bài này, em biết dùng for với range.',
  exercises: 'Mỗi bài tập gồm đề bài, ví dụ và bộ kiểm tra chấm bài. Hãy lưu bài học trước khi thêm bài tập.',
  statement: 'Cho số nguyên dương n. Hãy in ra bình phương của từng số từ 1 đến n.',
  examples: 'Dữ liệu vào: 3. Kết quả ra: 1, 4, 9 (mỗi số một dòng).',
  tests: 'Dữ liệu vào: 5. Kết quả đúng: 1, 4, 9, 16, 25 (mỗi số một dòng).',
  solution: 'for i in range(1, n + 1): print(i * i)',
};
