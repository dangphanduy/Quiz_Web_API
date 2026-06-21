// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

(function () {
    const storageKey = 'ymedu.language';
    const defaultLanguage = 'vi';

    const translations = {
        vi: {
            'language.vi': 'Tiếng Việt',
            'language.en': 'English',
            'nav.explore': 'Khám phá',
            'nav.all': 'Tất cả',
            'nav.searchPlaceholder': 'Tìm kiếm khóa học...',
            'nav.subscription': 'Gói thuê bao',
            'nav.instructor': 'Giảng viên',
            'nav.learning': 'Học tập',
            'nav.myCart': 'Giỏ hàng của tôi',
            'nav.adminDashboard': 'Bảng điều khiển admin',
            'nav.instructorDashboard': 'Bảng điều khiển của giảng viên',
            'nav.accountSettings': 'Cài đặt tài khoản',
            'nav.editProfile': 'Chỉnh sửa hồ sơ',
            'nav.purchaseHistory': 'Lịch sử mua hàng',
            'nav.language': 'Ngôn ngữ',
            'nav.logout': 'Đăng xuất',
            'nav.login': 'Đăng nhập',
            'nav.register': 'Đăng ký'
        },
        en: {
            'language.vi': 'Vietnamese',
            'language.en': 'English',
            'nav.explore': 'Explore',
            'nav.all': 'All',
            'nav.searchPlaceholder': 'Search for courses...',
            'nav.subscription': 'Plans',
            'nav.instructor': 'Instructor',
            'nav.learning': 'Learning',
            'nav.myCart': 'My cart',
            'nav.adminDashboard': 'Admin dashboard',
            'nav.instructorDashboard': 'Instructor dashboard',
            'nav.accountSettings': 'Account settings',
            'nav.editProfile': 'Edit profile',
            'nav.purchaseHistory': 'Purchase history',
            'nav.language': 'Language',
            'nav.logout': 'Log out',
            'nav.login': 'Log in',
            'nav.register': 'Sign up'
        }
    };

    function getSavedLanguage() {
        const saved = localStorage.getItem(storageKey);
        return translations[saved] ? saved : defaultLanguage;
    }

    function translateElement(element, language) {
        const dictionary = translations[language];
        const textKey = element.dataset.i18n;
        const placeholderKey = element.dataset.i18nPlaceholder;

        if (textKey && dictionary[textKey]) {
            element.textContent = dictionary[textKey];
        }

        if (placeholderKey && dictionary[placeholderKey]) {
            element.setAttribute('placeholder', dictionary[placeholderKey]);
        }
    }

    function applyLanguage(language) {
        if (!translations[language]) {
            language = defaultLanguage;
        }

        localStorage.setItem(storageKey, language);
        document.documentElement.lang = language;

        document.querySelectorAll('[data-i18n], [data-i18n-placeholder]').forEach(element => {
            translateElement(element, language);
        });

        document.querySelectorAll('[data-current-language-label]').forEach(element => {
            element.textContent = language === 'vi'
                ? translations.vi['language.vi']
                : translations.en['language.en'];
        });

        document.querySelectorAll('[data-language-option]').forEach(button => {
            const isActive = button.dataset.languageOption === language;
            button.classList.toggle('active', isActive);
            button.setAttribute('aria-pressed', isActive ? 'true' : 'false');
        });
    }

    document.addEventListener('DOMContentLoaded', function () {
        applyLanguage(getSavedLanguage());

        document.querySelectorAll('[data-language-option]').forEach(button => {
            button.addEventListener('click', function () {
                applyLanguage(this.dataset.languageOption);
            });
        });
    });
})();
