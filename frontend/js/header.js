// App shell DÙNG CHUNG cho mọi trang sau đăng nhập: thanh bên trái (aside#site-menu: logo, nhóm mục theo role, nút thu gọn)
// và thanh trên (header#site-header: nút Menu, vị trí hiện tại, tìm nhanh, đổi giao diện, menu người dùng).
// >= 900 px thanh bên cố định (264 px) hoặc rail (72 px, lưu ở localStorage th12.sidebar); dưới 900 px là ngăn kéo.
// Tên người dùng luôn chèn bằng textContent.
import { el } from '/js/dom.js';
import { api, homeFor } from '/js/api.js';
import { icon } from '/js/icons.js';
import { createThemeButton } from '/js/theme.js';
import { uiPrefs } from '/js/storage.js';

export function roleLabel(role) {
  return role === 'Teacher' ? 'Giáo viên' : 'Học sinh';
}

/** Nhóm mục điều hướng theo role; nguồn duy nhất cho thanh bên, breadcrumb và tìm nhanh. Thêm mục mới tại đây. */
export function navGroups(role) {
  if (role === 'Teacher') {
    return [
      { items: [{ key: 'overview', href: '/teacher/dashboard.html', text: 'Tổng quan', icon: 'grid' }] },
      {
        title: 'Soạn bài',
        items: [
          { key: 'lessons', href: '/teacher/index.html', text: 'Bài học của tôi', icon: 'book' },
          { key: 'assign', href: '/teacher/assignments.html', text: 'Giao bài', icon: 'send' },
        ],
      },
      {
        title: 'Lớp học',
        items: [
          { key: 'progress', href: '/teacher/progress.html', text: 'Tiến độ', icon: 'chart' },
          { key: 'students', href: '/teacher/students.html', text: 'Học sinh', icon: 'users' },
        ],
      },
      { title: 'Khác', items: [{ key: 'preview', href: '/lessons.html', text: 'Xem như học sinh (xem trước)', icon: 'eye' }] },
    ];
  }
  return [
    { items: [{ key: 'overview', href: '/dashboard.html', text: 'Tổng quan', icon: 'grid' }] },
    {
      title: 'Học tập',
      items: [
        { key: 'lessons', href: '/lessons.html', text: 'Bài học', icon: 'book' },
        { key: 'assignments', href: '/assignments.html', text: 'Bài tập của tôi', icon: 'clipboard' },
      ],
    },
  ];
}

// Cấp cuối của breadcrumb ở các trang sâu
const LEAF = {
  '/lesson.html': 'Đọc bài',
  '/problem.html': 'Làm bài',
  '/teacher/lesson-edit.html': 'Soạn bài học',
  '/teacher/exercise-edit.html': 'Soạn bài tập',
};

const FOCUSABLE = 'a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])';

let teardown = null;

function resolveActive(user, active) {
  // Giáo viên xem trang học sinh (xem trước) thì mục "Xem như học sinh" được đánh dấu
  if (user.role === 'Teacher' && !location.pathname.startsWith('/teacher/') && active === 'lessons') return 'preview';
  return active;
}

function ensure(id, tag, className, before) {
  let node = document.getElementById(id);
  if (!node) {
    node = el(tag, { id, class: className });
    before.parentNode.insertBefore(node, before);
  }
  return node;
}

export function renderHeader(user, activeKey = '') {
  const host = document.getElementById('site-header');
  if (!host) return;
  if (teardown) teardown();
  const side = ensure('site-menu', 'aside', 'app-sidebar', host);
  side.replaceChildren();
  host.replaceChildren();
  document.body.setAttribute('data-shell', '');

  const root = document.documentElement;
  const locked = user.mustChangePassword;
  const groups = locked ? [] : navGroups(user.role);
  const active = resolveActive(user, activeKey);
  const home = homeFor(user);
  const onProblem = /\/problem\.html$/i.test(location.pathname);
  host.setAttribute('data-role', user.role === 'Teacher' ? 'Teacher' : 'Student');
  if (!root.hasAttribute('data-sidebar')) root.setAttribute('data-sidebar', uiPrefs.getSidebar());

  // ---------------- Thanh bên ----------------
  const brand = el(
    'a',
    { class: 'brand', href: locked ? '/change-password.html' : home, 'aria-label': 'DK Edu – về trang chủ' },
    el('span', { class: 'brand__tile' }, el('img', { class: 'brand__logo', src: '/img/logo-dk-icon.png', alt: '', width: '46', height: '40' })),
    el('span', { class: 'brand__name' }, 'DK Edu'),
    el('span', { class: 'brand__tag' }, 'Tin học 12'),
  );
  const closeBtn = el('button', { type: 'button', class: 'btn btn--ghost app-sidebar__close', 'aria-label': 'Đóng menu' }, icon('x'));
  const links = [];
  const nav = groups.length
    ? el(
        'nav',
        { id: 'site-nav', class: 'site-nav', 'aria-label': 'Điều hướng chính' },
        groups.map((group, index) => {
          const headingId = 'app-group-' + index;
          return el(
            'div',
            { class: 'app-group' },
            group.title ? el('p', { class: 'app-group__title', id: headingId }, group.title) : null,
            el(
              'ul',
              { 'aria-labelledby': group.title ? headingId : null },
              group.items.map((item) => {
                const link = el(
                  'a',
                  { class: 'app-link', href: item.href, 'aria-current': item.key === active ? 'page' : null },
                  icon(item.icon),
                  el('span', { class: 'app-link__text' }, item.text),
                );
                link.dataset.label = item.text;
                links.push(link);
                return el('li', {}, link);
              }),
            ),
          );
        }),
      )
    : null;
  const collapseText = el('span', { class: 'app-link__text' }, 'Thu gọn thanh bên');
  const collapse = nav
    ? el('button', { type: 'button', class: 'app-collapse', 'aria-expanded': 'true' }, icon('chevrons-left'), collapseText)
    : null;
  side.append(el('div', { class: 'app-sidebar__top' }, brand, closeBtn), nav, collapse);

  // Rail: nhãn vẫn có trong DOM (ẩn bằng CSS), thêm tooltip title khi đang là rail để nhìn thấy tên
  const wide = window.matchMedia('(min-width: 900px)');
  function syncRail() {
    const rail = wide.matches && root.getAttribute('data-sidebar') === 'collapsed';
    links.forEach((link) => (rail ? link.setAttribute('title', link.dataset.label) : link.removeAttribute('title')));
    if (collapse) {
      collapse.setAttribute('aria-expanded', String(!rail));
      collapseText.textContent = rail ? 'Mở rộng thanh bên' : 'Thu gọn thanh bên';
      if (rail) collapse.setAttribute('title', 'Mở rộng thanh bên');
      else collapse.removeAttribute('title');
    }
  }
  if (collapse) {
    collapse.addEventListener('click', () => {
      const next = root.getAttribute('data-sidebar') === 'collapsed' ? 'expanded' : 'collapsed';
      root.setAttribute('data-sidebar', next);
      if (!onProblem) uiPrefs.setSidebar(next);
      syncRail();
    });
  }

  // ---------------- Thanh trên ----------------
  const toggle = el('button', { type: 'button', class: 'btn nav-toggle', 'aria-expanded': 'false', 'aria-controls': 'site-menu' }, icon('menu'), 'Menu');
  if (!nav) toggle.hidden = true;

  const leaf = LEAF[location.pathname.toLowerCase()] || null;
  let head = null;
  for (const group of groups) for (const item of group.items) if (item.key === active) head = item;
  const crumbItems = [];
  if (head) {
    crumbItems.push(leaf ? el('a', { href: head.href }, head.text) : head.text);
  } else if (active === 'password') {
    crumbItems.push('Đổi mật khẩu');
  }
  if (leaf && head) crumbItems.push(leaf);
  const crumbs = el(
    'nav',
    { class: 'app-crumbs', 'aria-label': 'Vị trí hiện tại' },
    el('ol', {}, crumbItems.map((content, index) => el('li', { 'aria-current': index === crumbItems.length - 1 ? 'page' : null }, content))),
  );

  const searchBtn = nav
    ? el(
        'button',
        { type: 'button', class: 'btn app-search', id: 'btn-search', 'aria-keyshortcuts': 'Control+K' },
        icon('search'),
        el('span', { class: 'app-search__text' }, 'Tìm nhanh'),
        el('kbd', { class: 'app-search__kbd', 'aria-hidden': 'true' }, 'Ctrl K'),
      )
    : null;

  const themeButton = createThemeButton();

  // Menu người dùng (disclosure): mở menu rồi bấm Đăng xuất là tối đa 2 thao tác
  const logoutText = el('span', {}, 'Đăng xuất');
  const logout = el('button', { type: 'button', class: 'app-menu__item', id: 'btn-logout' }, icon('log-out'), logoutText);
  const passwordLink = locked
    ? null
    : el('a', { class: 'app-menu__item', href: '/change-password.html', 'aria-current': activeKey === 'password' ? 'page' : null }, icon('key'), el('span', {}, 'Đổi mật khẩu'));
  const panel = el(
    'div',
    { class: 'app-menu', id: 'app-user-menu', hidden: true },
    el('div', { class: 'app-menu__head' }, el('strong', { class: 'app-menu__name' }, user.displayName), el('span', { class: 'role-badge' }, roleLabel(user.role))),
    passwordLink,
    logout,
  );
  const userBtn = el(
    'button',
    { type: 'button', class: 'btn app-user__btn', 'aria-haspopup': 'true', 'aria-expanded': 'false', 'aria-controls': 'app-user-menu' },
    el('span', { class: 'app-user__avatar', 'aria-hidden': 'true' }, icon('user')),
    el(
      'span',
      { class: 'app-user__meta' },
      el('span', { class: 'app-user__name', title: user.displayName }, user.displayName),
      el('span', { class: 'role-badge' }, roleLabel(user.role)),
    ),
    icon('chevron-down', 'app-user__caret'),
  );
  const userBox = el('div', { class: 'app-user' }, userBtn, panel);

  host.append(el('div', { class: 'app-topbar' }, toggle, crumbs, el('div', { class: 'app-topbar__tools' }, searchBtn, themeButton, userBox)));

  // ---------------- Ngăn kéo dưới 900 px ----------------
  const scrim = el('div', { class: 'app-scrim' });
  document.body.append(scrim);
  let drawerOpen = false;
  let inerted = [];

  const sideFocusables = () => Array.from(side.querySelectorAll(FOCUSABLE)).filter((n) => !n.hidden && n.getClientRects().length > 0);

  function onDrawerKey(event) {
    if (event.key === 'Escape') {
      event.preventDefault();
      closeDrawer(true);
    } else if (event.key === 'Tab') {
      const items = sideFocusables();
      if (!items.length) return;
      const index = items.indexOf(document.activeElement);
      event.preventDefault();
      if (event.shiftKey) items[index <= 0 ? items.length - 1 : index - 1].focus();
      else items[(index + 1) % items.length].focus();
    }
  }

  function openDrawer() {
    if (drawerOpen || wide.matches) return;
    drawerOpen = true;
    for (const child of Array.from(document.body.children)) {
      if (child !== side && child !== scrim && child.id !== 'toast-region' && !child.hasAttribute('inert')) {
        child.setAttribute('inert', '');
        inerted.push(child);
      }
    }
    side.inert = false;
    side.classList.add('is-open');
    scrim.classList.add('is-open');
    toggle.setAttribute('aria-expanded', 'true');
    root.classList.add('has-drawer');
    document.addEventListener('keydown', onDrawerKey, true);
    (sideFocusables()[0] || closeBtn).focus();
  }

  function closeDrawer(restoreFocus) {
    if (!drawerOpen) return;
    drawerOpen = false;
    document.removeEventListener('keydown', onDrawerKey, true);
    side.classList.remove('is-open');
    scrim.classList.remove('is-open');
    toggle.setAttribute('aria-expanded', 'false');
    root.classList.remove('has-drawer');
    inerted.forEach((n) => n.removeAttribute('inert'));
    inerted = [];
    side.inert = !wide.matches;
    if (restoreFocus) toggle.focus();
  }

  function place() {
    if (wide.matches) closeDrawer(false);
    side.inert = !wide.matches && !drawerOpen;
    syncRail();
  }

  toggle.addEventListener('click', () => (drawerOpen ? closeDrawer(true) : openDrawer()));
  closeBtn.addEventListener('click', () => closeDrawer(true));
  scrim.addEventListener('click', () => closeDrawer(true));
  wide.addEventListener('change', place);

  // ---------------- Menu người dùng ----------------
  const menuItems = () => Array.from(panel.querySelectorAll('a, button'));

  function onOutside(event) {
    if (!userBox.contains(event.target)) closeMenu(false);
  }

  function openMenu(focusFirst) {
    panel.hidden = false;
    userBtn.setAttribute('aria-expanded', 'true');
    document.addEventListener('pointerdown', onOutside);
    if (focusFirst) menuItems()[0].focus();
  }

  function closeMenu(restoreFocus) {
    if (panel.hidden) return;
    panel.hidden = true;
    userBtn.setAttribute('aria-expanded', 'false');
    document.removeEventListener('pointerdown', onOutside);
    if (restoreFocus) userBtn.focus();
  }

  userBtn.addEventListener('click', () => (panel.hidden ? openMenu(false) : closeMenu(false)));
  userBtn.addEventListener('keydown', (event) => {
    if (event.key === 'ArrowDown') {
      event.preventDefault();
      event.stopPropagation();
      if (panel.hidden) openMenu(true);
      else menuItems()[0].focus();
    }
  });
  userBox.addEventListener('keydown', (event) => {
    if (panel.hidden) return;
    const items = menuItems();
    const index = items.indexOf(document.activeElement);
    if (event.key === 'Escape') {
      event.preventDefault();
      closeMenu(true);
    } else if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
      event.preventDefault();
      const step = event.key === 'ArrowDown' ? 1 : -1;
      items[(index + step + items.length) % items.length].focus();
    } else if (event.key === 'Home' || event.key === 'End') {
      event.preventDefault();
      items[event.key === 'Home' ? 0 : items.length - 1].focus();
    }
  });
  userBox.addEventListener('focusout', (event) => {
    if (event.relatedTarget && !userBox.contains(event.relatedTarget)) closeMenu(false);
  });

  logout.addEventListener('click', async () => {
    logout.disabled = true;
    logout.setAttribute('aria-busy', 'true');
    logoutText.textContent = 'Đang đăng xuất…';
    try {
      await api.logout();
    } catch (e) {
      /* dù lỗi mạng vẫn chuyển về trang đăng nhập; phiên cũ sẽ hết hạn */
    }
    location.href = '/login.html';
  });

  // ---------------- Tìm nhanh (nạp mã khi mở lần đầu) ----------------
  let searching = false;
  async function openSearch() {
    if (searching || root.classList.contains('has-modal')) return;
    searching = true;
    closeDrawer(false);
    closeMenu(false);
    try {
      const { openQuickSearch } = await import('/js/search.js');
      await openQuickSearch(user, searchBtn);
    } finally {
      searching = false;
    }
  }
  function onShortcut(event) {
    if ((event.ctrlKey || event.metaKey) && !event.altKey && event.key.toLowerCase() === 'k') {
      event.preventDefault();
      openSearch();
    }
  }
  if (searchBtn) {
    searchBtn.addEventListener('click', openSearch);
    document.addEventListener('keydown', onShortcut);
  }

  place();

  teardown = () => {
    closeDrawer(false);
    closeMenu(false);
    wide.removeEventListener('change', place);
    document.removeEventListener('keydown', onShortcut);
    scrim.remove();
    teardown = null;
  };
  window.addEventListener('pagehide', () => {
    if (teardown) teardown();
  }, { once: true });
}
