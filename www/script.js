/**
 * Conductor Website Interactive Features
 */

(function() {
    'use strict';

    // ============================================
    // Theme Management
    // ============================================
    const ThemeManager = {
        STORAGE_KEY: 'conductor-theme',

        init() {
            // Check for saved theme preference or system preference
            const savedTheme = localStorage.getItem(this.STORAGE_KEY);
            const systemPrefersDark = window.matchMedia('(prefers-color-scheme: dark)').matches;

            if (savedTheme) {
                this.setTheme(savedTheme);
            } else if (systemPrefersDark) {
                this.setTheme('dark');
            } else {
                this.setTheme('light');
            }

            // Listen for system theme changes
            window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', (e) => {
                if (!localStorage.getItem(this.STORAGE_KEY)) {
                    this.setTheme(e.matches ? 'dark' : 'light');
                }
            });

            // Set up toggle button
            const toggleBtn = document.getElementById('theme-toggle');
            if (toggleBtn) {
                toggleBtn.addEventListener('click', () => this.toggle());
            }
        },

        setTheme(theme) {
            document.documentElement.setAttribute('data-theme', theme);
            localStorage.setItem(this.STORAGE_KEY, theme);

            // Update favicon based on theme (inverted for contrast)
            const faviconHref = theme === 'dark' ? 'icon-light.ico' : 'icon-dark.ico';
            document.querySelectorAll('link[rel="icon"], link[rel="shortcut icon"]').forEach(link => {
                link.href = faviconHref;
            });
        },

        toggle() {
            const currentTheme = document.documentElement.getAttribute('data-theme');
            const newTheme = currentTheme === 'dark' ? 'light' : 'dark';
            this.setTheme(newTheme);
        }
    };

    // ============================================
    // Navigation
    // ============================================
    const Navigation = {
        init() {
            this.navbar = document.querySelector('.navbar');
            this.mobileNav = document.getElementById('mobile-nav');
            this.navToggle = document.getElementById('nav-toggle');

            // Mobile menu toggle
            if (this.navToggle && this.mobileNav) {
                this.navToggle.addEventListener('click', () => this.toggleMobileMenu());

                // Close mobile menu when clicking a link
                this.mobileNav.querySelectorAll('a').forEach(link => {
                    link.addEventListener('click', () => this.closeMobileMenu());
                });
            }

            // Scroll effects
            this.handleScroll();
            window.addEventListener('scroll', () => this.handleScroll(), { passive: true });

            // Smooth scroll for anchor links
            document.querySelectorAll('a[href^="#"]').forEach(anchor => {
                anchor.addEventListener('click', (e) => this.smoothScroll(e));
            });
        },

        toggleMobileMenu() {
            this.mobileNav.classList.toggle('active');
            this.navToggle.classList.toggle('active');
        },

        closeMobileMenu() {
            this.mobileNav.classList.remove('active');
            this.navToggle.classList.remove('active');
        },

        handleScroll() {
            if (window.scrollY > 50) {
                this.navbar.classList.add('scrolled');
            } else {
                this.navbar.classList.remove('scrolled');
            }
        },

        smoothScroll(e) {
            const href = e.currentTarget.getAttribute('href');
            if (href === '#') return;

            const target = document.querySelector(href);
            if (target) {
                e.preventDefault();
                const navbarHeight = this.navbar.offsetHeight;
                const targetPosition = target.offsetTop - navbarHeight;

                window.scrollTo({
                    top: targetPosition,
                    behavior: 'smooth'
                });

                // Update URL without triggering scroll
                history.pushState(null, null, href);
            }
        }
    };

    // ============================================
    // Scroll Animations
    // ============================================
    const ScrollAnimations = {
        init() {
            // Add fade-in class to animatable elements
            const animatableSelectors = [
                '.feature-card',
                '.security-card',
                '.use-case-card',
                '.database-card',
                '.dashboard-feature',
                '.vmr-card',
                '.api-card',
                '.health-feature',
                '.stat-card'
            ];

            animatableSelectors.forEach(selector => {
                document.querySelectorAll(selector).forEach(el => {
                    el.classList.add('fade-in');
                });
            });

            // Set up intersection observer
            this.observer = new IntersectionObserver(
                (entries) => this.handleIntersection(entries),
                {
                    root: null,
                    rootMargin: '0px 0px -50px 0px',
                    threshold: 0.1
                }
            );

            // Observe all fade-in elements
            document.querySelectorAll('.fade-in').forEach(el => {
                this.observer.observe(el);
            });
        },

        handleIntersection(entries) {
            entries.forEach(entry => {
                if (entry.isIntersecting) {
                    // Add staggered delay based on element index within its parent
                    const siblings = Array.from(entry.target.parentElement.children)
                        .filter(child => child.classList.contains('fade-in'));
                    const index = siblings.indexOf(entry.target);

                    setTimeout(() => {
                        entry.target.classList.add('visible');
                    }, index * 100);

                    // Stop observing once visible
                    this.observer.unobserve(entry.target);
                }
            });
        }
    };

    // ============================================
    // Code Copy Functionality
    // ============================================
    const CodeCopy = {
        init() {
            // Already handled by inline onclick, but let's add fallback
            window.copyCode = this.copyCode.bind(this);
        },

        async copyCode(button) {
            const codeBlock = button.closest('.code-block');
            const code = codeBlock.querySelector('code');
            const text = code.textContent;

            try {
                await navigator.clipboard.writeText(text);

                // Visual feedback
                const originalText = button.textContent;
                button.textContent = 'Copied!';
                button.classList.add('copied');

                setTimeout(() => {
                    button.textContent = originalText;
                    button.classList.remove('copied');
                }, 2000);
            } catch (err) {
                console.error('Failed to copy:', err);

                // Fallback for older browsers
                const textArea = document.createElement('textarea');
                textArea.value = text;
                textArea.style.position = 'fixed';
                textArea.style.left = '-9999px';
                document.body.appendChild(textArea);
                textArea.select();

                try {
                    document.execCommand('copy');
                    button.textContent = 'Copied!';
                    button.classList.add('copied');

                    setTimeout(() => {
                        button.textContent = 'Copy';
                        button.classList.remove('copied');
                    }, 2000);
                } catch (e) {
                    console.error('Fallback copy failed:', e);
                }

                document.body.removeChild(textArea);
            }
        }
    };

    // ============================================
    // Architecture Diagram Animation
    // ============================================
    const DiagramAnimation = {
        init() {
            const diagram = document.querySelector('.architecture-diagram');
            if (!diagram) return;

            // Add subtle animation on hover
            const conductorBox = diagram.querySelector('.conductor-box');
            if (conductorBox) {
                conductorBox.addEventListener('mouseenter', () => {
                    conductorBox.style.transform = 'scale(1.02)';
                    conductorBox.style.transition = 'transform 0.3s ease';
                });

                conductorBox.addEventListener('mouseleave', () => {
                    conductorBox.style.transform = 'scale(1)';
                });
            }
        }
    };

    // ============================================
    // VMR Status Animation
    // ============================================
    const VMRStatusAnimation = {
        init() {
            const statusDots = document.querySelectorAll('.status-dot.healthy');
            statusDots.forEach(dot => {
                // Add subtle pulse animation
                dot.style.animation = 'pulse 2s ease-in-out infinite';
            });

            // Add CSS for pulse animation if not exists
            if (!document.getElementById('pulse-animation')) {
                const style = document.createElement('style');
                style.id = 'pulse-animation';
                style.textContent = `
                    @keyframes pulse {
                        0%, 100% { opacity: 1; transform: scale(1); }
                        50% { opacity: 0.7; transform: scale(1.1); }
                    }
                `;
                document.head.appendChild(style);
            }
        }
    };

    // ============================================
    // Keyboard Navigation
    // ============================================
    const KeyboardNav = {
        init() {
            // Add keyboard support for buttons and interactive elements
            document.addEventListener('keydown', (e) => {
                if (e.key === 'Escape') {
                    // Close mobile menu
                    const mobileNav = document.getElementById('mobile-nav');
                    if (mobileNav && mobileNav.classList.contains('active')) {
                        Navigation.closeMobileMenu();
                    }
                }
            });
        }
    };

    // ============================================
    // Performance Monitoring
    // ============================================
    const Performance = {
        init() {
            // Log performance metrics for debugging
            if (window.performance && window.performance.timing) {
                window.addEventListener('load', () => {
                    const timing = window.performance.timing;
                    const loadTime = timing.loadEventEnd - timing.navigationStart;
                    console.log(`Page load time: ${loadTime}ms`);
                });
            }
        }
    };

    // ============================================
    // External Link Handler
    // ============================================
    const ExternalLinks = {
        init() {
            // Ensure all external links open in new tab with proper security
            document.querySelectorAll('a[target="_blank"]').forEach(link => {
                if (!link.getAttribute('rel')) {
                    link.setAttribute('rel', 'noopener noreferrer');
                }
            });
        }
    };

    // ============================================
    // Initialize All Modules
    // ============================================
    function init() {
        ThemeManager.init();
        Navigation.init();
        ScrollAnimations.init();
        CodeCopy.init();
        DiagramAnimation.init();
        VMRStatusAnimation.init();
        KeyboardNav.init();
        ExternalLinks.init();

        // Performance monitoring (development only)
        if (location.hostname === 'localhost' || location.hostname === '127.0.0.1') {
            Performance.init();
        }

        console.log('Conductor website initialized');
    }

    // Run initialization when DOM is ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
