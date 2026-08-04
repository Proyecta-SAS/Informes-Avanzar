const areas = [
  {
    id: "comercial",
    icon: "↗",
    title: "Comercial",
    description: "Seguimiento de oportunidades, radicación y desempeño de la fuerza comercial.",
    tone: "violet",
    reports: [
      { title: "Informe General Comercial", description: "Radicación, negociaciones, comisiones, cartera, embudos y etapas en una sola vista.", href: "/reporte.html?id=informe_general_comercial", badge: "Informe general" },
      { title: "RCH Comercial", description: "Negociaciones y avance de la pipeline comercial RCH.", href: "/reporte.html?id=rch_comercial", badge: "Pipeline" },
      { title: "Fuerza Comercial", description: "Radicación, cartera, comisiones, embudos y liderazgo.", href: "/reporte.html?id=fuerza_comercial_diego", badge: "Informe ejecutivo" }
    ]
  },
  {
    id: "pnnc",
    icon: "◇",
    title: "PNNC",
    description: "Vista comercial y operativa de insolvencia y negociación de cartera.",
    tone: "blue",
    reports: [
      { title: "PNNC Comercial", description: "Prospectos, seguimiento y conversión comercial PNNC.", href: "/reporte.html?id=pnnc_comercial", badge: "Comercial" },
      { title: "PNNC Operativa", description: "Casos, documentación y etapas operativas PNNC.", href: "/reporte.html?id=pnnc_operativa", badge: "Operativa" }
    ]
  },
  {
    id: "operaciones",
    icon: "⚙",
    title: "Operativa",
    description: "Control operativo, documentación y avance de los casos radicados.",
    tone: "red",
    reports: [
      { title: "RCH Operativa", description: "Gestión operativa de negociaciones y etapas RCH.", href: "/reporte.html?id=rch_operativa", badge: "Pipeline" }
    ]
  }
];

const renderAreas = (query = "") => {
  const normalized = query.trim().toLocaleLowerCase("es-CO");
  let visibleReports = 0;
  const content = areas.map((area) => {
    const areaMatch = `${area.title} ${area.description}`.toLocaleLowerCase("es-CO").includes(normalized);
    const reports = area.reports.filter((report) => areaMatch || `${report.title} ${report.description} ${report.badge}`.toLocaleLowerCase("es-CO").includes(normalized));
    if (!reports.length) return "";
    visibleReports += reports.length;
    return `<article id="${area.id}" class="home-area-card ${area.tone}">
      <header><span>${area.icon}</span><div><small>Área</small><h3>${area.title}</h3></div><em>${reports.length} ${reports.length === 1 ? "informe" : "informes"}</em></header>
      <p>${area.description}</p>
      <div class="home-report-links">${reports.map((report) => `<a href="${report.href}"><div><span>${report.badge}</span><strong>${report.title}</strong><small>${report.description}</small></div><b>→</b></a>`).join("")}</div>
    </article>`;
  }).join("");
  document.getElementById("homeAreas").innerHTML = content || `<div class="home-no-results"><strong>No encontramos informes</strong><span>Prueba con otro nombre o área.</span></div>`;
  document.getElementById("homeResultCount").textContent = `${visibleReports} ${visibleReports === 1 ? "acceso" : "accesos"}`;
};

document.getElementById("reportSearch").addEventListener("input", (event) => renderAreas(event.target.value));
renderAreas();
