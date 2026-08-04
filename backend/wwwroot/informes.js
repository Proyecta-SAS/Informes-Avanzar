const reportAreas = [
  {
    id: "comercial",
    icon: "↗",
    title: "Comercial",
    tone: "violet",
    description: "Seguimiento de oportunidades, radicación y desempeño de la fuerza comercial.",
    reports: [
      { id: "informe_general_comercial", title: "Informe General Comercial", badge: "Informe general", description: "Radicación, negociaciones, comisiones, cartera, embudos y etapas en una sola vista." },
      { id: "rch_comercial", title: "RCH Comercial", badge: "Pipeline", description: "Negociaciones y avance de la pipeline comercial RCH." },
      { id: "fuerza_comercial_diego", title: "Fuerza Comercial", badge: "Informe ejecutivo", description: "Radicación, cartera, comisiones, embudos y liderazgo." }
    ]
  },
  {
    id: "pnnc",
    icon: "◇",
    title: "PNNC",
    tone: "blue",
    description: "Vista comercial y operativa de insolvencia y negociación de cartera.",
    reports: [
      { id: "pnnc_comercial", title: "PNNC Comercial", badge: "Comercial", description: "Prospectos, seguimiento y conversión comercial PNNC." },
      { id: "pnnc_operativa", title: "PNNC Operativa", badge: "Operativa", description: "Casos, documentación y etapas operativas PNNC." }
    ]
  },
  {
    id: "operaciones",
    icon: "⚙",
    title: "Operativa",
    tone: "red",
    description: "Control operativo, documentación y avance de los casos radicados.",
    reports: [
      { id: "rch_operativa", title: "RCH Operativa", badge: "Pipeline", description: "Gestión operativa de negociaciones y etapas RCH." }
    ]
  }
];

const renderReports = (query = "") => {
  const normalized = query.trim().toLocaleLowerCase("es-CO");
  let visibleReports = 0;
  const content = reportAreas.map((area) => {
    const areaMatches = area.title.toLocaleLowerCase("es-CO").includes(normalized);
    const reports = area.reports.filter((report) => areaMatches
      || `${report.title} ${report.badge} ${report.description}`.toLocaleLowerCase("es-CO").includes(normalized));
    if (!reports.length) return "";
    visibleReports += reports.length;
    return `<article id="${area.id}" class="catalog-area-card ${area.tone}">
      <header><span>${area.icon}</span><div><small>Área</small><h3>${area.title}</h3></div><em>${reports.length} ${reports.length === 1 ? "informe" : "informes"}</em></header>
      <p>${area.description}</p>
      <div class="catalog-report-links">${reports.map((report) => `<a href="/reporte.html?id=${report.id}"><div><span>${report.badge}</span><strong>${report.title}</strong><small>${report.description}</small></div><b>→</b></a>`).join("")}</div>
    </article>`;
  }).join("");

  document.getElementById("visibleReports").textContent = visibleReports;
  document.getElementById("dashboardGrid").innerHTML = content
    || `<div class="catalog-empty"><strong>No encontramos informes</strong><span>Prueba con otro nombre o área.</span></div>`;
};

document.getElementById("dashboardSearch").addEventListener("input", (event) => renderReports(event.target.value));
renderReports();
