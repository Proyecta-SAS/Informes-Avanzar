const sessionMenu = document.querySelector(".sidebar .menu");
if (sessionMenu) {
  const logoutButton = document.createElement("button");
  logoutButton.className = "sidebar-logout";
  logoutButton.type = "button";
  logoutButton.innerHTML = "<span>↪</span>Cerrar sesión";
  logoutButton.addEventListener("click", async () => {
    await fetch("/api/auth/logout", { method: "POST" });
    sessionStorage.removeItem("adminAccessKey");
    location.href = "/login.html";
  });
  sessionMenu.append(logoutButton);
}
