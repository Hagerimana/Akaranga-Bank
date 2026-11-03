(() => {
  const root = document.documentElement;
  const themeButton = document.getElementById("login-theme-toggle");
  const passwordInput = document.getElementById("Input_Password");
  const passwordButton = document.getElementById("login-password-toggle");

  const updateThemeButton = () => {
    if (!themeButton) return;

    const dark = root.dataset.theme === "dark";

    themeButton.textContent = dark ? "Mode clair" : "Mode sombre";
    themeButton.setAttribute(
      "aria-label",
      dark ? "Activer le thème clair" : "Activer le thème sombre",
    );
  };

  updateThemeButton();

  themeButton?.addEventListener("click", () => {
    const next = root.dataset.theme === "dark" ? "light" : "dark";

    root.dataset.theme = next;

    try {
      localStorage.setItem("ak-theme", next);
    } catch {
      // Le changement visuel fonctionne sans persistance.
    }

    updateThemeButton();
  });

  passwordButton?.addEventListener("click", () => {
    if (!passwordInput) return;

    const show = passwordInput.type === "password";

    passwordInput.type = show ? "text" : "password";
    passwordButton.textContent = show ? "Masquer" : "Afficher";

    passwordButton.setAttribute("aria-pressed", String(show));
    passwordButton.setAttribute(
      "aria-label",
      show ? "Masquer le mot de passe" : "Afficher le mot de passe",
    );
  });
})();
