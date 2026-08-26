/**
 * ==============================================================================
 * SyncPulse Technical Landing & Showcase Controller
 * Smooth Scroll, Active Navigation Highlighting & Multi-Theme Engine
 * ==============================================================================
 */

const themes = ["dark", "light", "matrix"];
let currentThemeIndex = 0;

document.addEventListener('DOMContentLoaded', () => {
    setupScrollSpy();
    restoreSavedTheme();
});

/**
 * Multi-Theme Engine
 */
function cycleTheme() {
    currentThemeIndex = (currentThemeIndex + 1) % themes.length;
    const selectedTheme = themes[currentThemeIndex];
    document.documentElement.setAttribute('data-theme', selectedTheme);
    localStorage.setItem('syncpulse_landing_theme', selectedTheme);
}

function restoreSavedTheme() {
    const saved = localStorage.getItem('syncpulse_landing_theme');
    if (saved && themes.includes(saved)) {
        currentThemeIndex = themes.indexOf(saved);
        document.documentElement.setAttribute('data-theme', saved);
    }
}

/**
 * ScrollSpy: Highlight active navigation link on scroll
 */
function setupScrollSpy() {
    const sections = document.querySelectorAll('section[id]');
    const navLinks = document.querySelectorAll('.nav-link-anchor');

    window.addEventListener('scroll', () => {
        let currentSectionId = '';
        const scrollPosition = window.scrollY + 120;

        sections.forEach(section => {
            const sectionTop = section.offsetTop;
            const sectionHeight = section.offsetHeight;

            if (scrollPosition >= sectionTop && scrollPosition < sectionTop + sectionHeight) {
                currentSectionId = section.getAttribute('id');
            }
        });

        navLinks.forEach(link => {
            link.classList.remove('active');
            if (link.getAttribute('href') === `#${currentSectionId}`) {
                link.classList.add('active');
            }
        });
    }, { passive: true });
}
