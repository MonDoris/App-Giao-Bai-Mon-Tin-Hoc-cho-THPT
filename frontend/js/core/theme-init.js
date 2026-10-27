// Đặt giao diện sáng/tối và trạng thái thanh bên trước lần vẽ đầu tiên. Tệp thường, nạp đồng bộ trong <head>.
(function () {
  var root = document.documentElement;
  var pref = 'auto';
  var side = 'expanded';
  try {
    try {
      var saved = window.localStorage.getItem('th12.theme');
      if (saved === 'light' || saved === 'dark') pref = saved;
      var bar = window.localStorage.getItem('th12.sidebar');
      if (bar === 'collapsed' || bar === 'expanded') side = bar;
    } catch (e) { /* bị chặn: giữ mặc định */ }
    var dark = pref === 'dark' || (pref === 'auto' && window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches);
    root.setAttribute('data-theme', dark ? 'dark' : 'light');
    root.setAttribute('data-theme-pref', pref);
    // Trang làm bài cần chỗ rộng: luôn bắt đầu ở dạng rail
    if (/\/problem\.html$/i.test(location.pathname)) side = 'collapsed';
    root.setAttribute('data-sidebar', side);
  } catch (e) { /* giữ mặc định của CSS */ }
})();
