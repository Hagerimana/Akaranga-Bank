(() => {
  const body = document.body;
  const sidebar = document.getElementById("akSidebar");
  const backdrop = document.getElementById("akBackdrop");
  const toggleSidebar = document.getElementById("akToggleSidebar");
  const themeToggle = document.getElementById("akThemeToggle");
  const themeIcon = document.getElementById("akThemeIcon");

  const isMobile = () => window.innerWidth < 992;

  const applyTheme = (theme) => {
    document.documentElement.setAttribute("data-theme", theme);
    document.documentElement.setAttribute("data-bs-theme", theme);
    localStorage.setItem("ak-theme", theme);

    if (themeIcon) {
      themeIcon.className = theme === "dark" ? "bi bi-sun" : "bi bi-moon-stars";
    }
  };

  const savedTheme = localStorage.getItem("ak-theme") ?? "light";
  applyTheme(savedTheme);

  toggleSidebar?.addEventListener("click", () => {
    if (isMobile()) {
      body.classList.toggle("ak-sidebar-open");
    } else {
      body.classList.toggle("ak-sidebar-collapsed");
    }
  });

  backdrop?.addEventListener("click", () => {
    body.classList.remove("ak-sidebar-open");
  });

  themeToggle?.addEventListener("click", () => {
    const current =
      document.documentElement.getAttribute("data-theme") ?? "light";

    applyTheme(current === "dark" ? "light" : "dark");
  });

  document.querySelectorAll(".ak-toggle-password").forEach((button) => {
    button.addEventListener("click", () => {
      const input = document.querySelector(button.dataset.target);
      const icon = button.querySelector("i");

      if (!input) return;

      const visible = input.type === "text";
      input.type = visible ? "password" : "text";

      icon?.classList.toggle("bi-eye", visible);
      icon?.classList.toggle("bi-eye-slash", !visible);
    });
  });

  document.querySelectorAll(".ak-nav-link").forEach((link) => {
    if (link.getAttribute("href") === window.location.pathname) {
      link.classList.add("active");
    }
  });
})();
