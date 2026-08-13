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
    const reportCodes = session?.accessibleReportCodes ?? [];
    if (session?.roleCode !== "admin" && reportCodes.length === 1) {
      location.href = `/reporte.html?id=${encodeURIComponent(reportCodes[0])}`;
      return;
    }

    const target = new URLSearchParams(location.search).get("returnUrl");
    location.href = target?.startsWith("/") ? target : "/";
  } catch (exception) {
    error.textContent = exception.message;
    error.hidden = false;
    button.disabled = false;
    button.textContent = "Ingresar al panel";
  }
});
