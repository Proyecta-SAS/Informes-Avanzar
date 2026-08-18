(() => {
  if (window.__avanzarThemeReady) return;
  window.__avanzarThemeReady = true;
  const key = "avanzar-panel-theme";
  const root = document.documentElement;
  root.dataset.theme = localStorage.getItem(key) === "dark" ? "dark" : "light";
  const mount = () => {
    const button = document.createElement("button");
    button.type = "button";
    button.className = "theme-toggle";
    const render = () => {
      const dark = root.dataset.theme === "dark";
      button.setAttribute("aria-label", dark ? "Cambiar a modo claro" : "Cambiar a modo oscuro");
      button.setAttribute("aria-pressed", String(dark));
      button.innerHTML = `<span aria-hidden="true">${dark ? "☀" : "☾"}</span><b>${dark ? "Modo claro" : "Modo oscuro"}</b>`;
    };
    button.addEventListener("click", () => {
      root.dataset.theme = root.dataset.theme === "dark" ? "light" : "dark";
      localStorage.setItem(key, root.dataset.theme);
      render();
    });
    render();
    const host = document.querySelector(".topbar-actions, .home-top-actions, .topbar, .home-topbar");
    if (host) host.appendChild(button);
    else {
      button.classList.add("theme-toggle-floating");
      document.body.appendChild(button);
    }
  };
  if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", mount, { once: true });
  else mount();
})();


document.getElementById("panelLoginForm").addEventListener("submit", async (event) => {
  event.preventDefault();
  const button = event.submitter;
  const error = document.getElementById("loginError");
  button.disabled = true;
  button.textContent = "Validando...";
  error.hidden = true;
  try {
    const response = await fetch("/api/auth/login", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        email: document.getElementById("loginEmail").value,
        password: document.getElementById("loginPassword").value
      })
    });
    if (!response.ok) throw new Error("Correo o contrasena incorrectos.");

    const sessionResponse = await fetch("/api/auth/me");
    const session = sessionResponse.ok ? await sessionResponse.json() : null;
    const target = new URLSearchParams(location.search).get("returnUrl");
    if (session?.roleCode === "admin" || session?.isSuperAdmin) {
      location.href = target?.startsWith("/") ? target : (session?.startUrl ?? "/");
      return;
    }
    location.href = session?.startUrl ?? "/informes.html?access=none";
  } catch (exception) {
    error.textContent = exception.message;
    error.hidden = false;
    button.disabled = false;
    button.textContent = "Ingresar al panel";
  }
});
