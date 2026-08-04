document.getElementById("panelLoginForm").addEventListener("submit", async (event) => {
  event.preventDefault();
  const button = event.submitter;
  const error = document.getElementById("loginError");
  button.disabled = true;
  button.textContent = "Validando…";
  error.hidden = true;
  try {
    const response = await fetch("/api/auth/login", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ email: document.getElementById("loginEmail").value, password: document.getElementById("loginPassword").value }) });
    if (!response.ok) throw new Error("Correo o contraseña incorrectos.");
    const target = new URLSearchParams(location.search).get("returnUrl");
    location.href = target?.startsWith("/") ? target : "/";
  } catch (exception) {
    error.textContent = exception.message;
    error.hidden = false;
    button.disabled = false;
    button.textContent = "Ingresar al panel";
  }
});
