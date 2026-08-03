const params = new URLSearchParams(window.location.search);
const reportId = params.get("id") ?? "rch_comercial";

const metadata = {
  rch_comercial: { name: "RCH Comercial", area: "Comercial", description: "Seguimiento comercial de negociaciones RCH." },
  rch_operativa: { name: "RCH Operativa", area: "Operaciones", description: "Seguimiento operativo de negociaciones RCH." },
  pnnc_comercial: { name: "PNNC Comercial", area: "Comercial", description: "Dashboard comercial PNNC." },
  pnnc_operativa: { name: "PNNC Operativa", area: "Operaciones", description: "Dashboard operativo PNNC." },
  fuerza_comercial_diego: {
    name: "Fuerza Comercial Diego",
    area: "Comercial",
    description: "Seguimiento de negociaciones, responsables, etapas y actividad de la fuerza comercial de Diego."
  }
};

const diegoSections = [
  {
    id: "radicacion",
    icon: "◎",
    title: "Radicación",
    description: "Seguimiento de valores, volumen y desempeño comercial durante 2026.",
    blocks: [
      ["Valores radicados por asesor", "Evolución mensual del total alcanzado por cada asesor.", "radicated"],
      ["Total de negociaciones por asesor", "Negociaciones, estudios, radicados y tasa de cierre.", "table"],
      ["Valores radicados por coordinador", "Comparativo mensual consolidado por coordinación.", "chart"],
      ["Valores radicados por líder", "Comparativo mensual consolidado por liderazgo.", "chart"],
      ["Detalle de coordinadores", "Resultados, porcentajes de cumplimiento y composición del equipo.", "table"],
      ["Detalle de radicaciones por líder", "Radicación y cumplimiento desagregados por líder.", "table"],
      ["Comisiones por asesor", "Base de liquidación y seguimiento de comisiones de 2026.", "table"]
    ]
  },
  {
    id: "carteras",
    icon: "$",
    title: "Estado de carteras",
    description: "Composición, recaudo y estado de la cartera comercial.",
    blocks: [
      ["Estado de cartera 2025", "Distribución entre cartera en tiempo, seguimiento y mora.", "donut"],
      ["Cartera recaudada", "Valor recaudado por asesor, líder y coordinación.", "chart"]
    ]
  },
  {
    id: "embudos",
    icon: "▽",
    title: "Embudos comerciales",
    description: "Conversión y concentración de oportunidades por etapa.",
    blocks: [
      ["Embudo Insolvencia", "Prospectos, contacto, seguimiento y recopilación de documentos.", "funnel"],
      ["Embudo RCH", "Avance desde prospección hasta recopilación de documentos.", "funnel"]
    ]
  },
  {
    id: "etapas",
    icon: "≡",
    title: "Etapas de pipelines",
    description: "Casos y valor comercial en las etapas prioritarias de RCH y PNNC.",
    blocks: [
      ["Etapas Comercial RCH", "Casos y valor por etapa comercial RCH.", "bars"],
      ["Etapas Operativa RCH", "Radicación por validar y documentación pendiente o subsanada.", "bars"],
      ["Etapas Comercial PNNC", "Recopilación, anticipo y cuarentena.", "bars"],
      ["Etapas Operativa PNNC", "Validación y estado de la documentación comercial.", "bars"]
    ]
  }
];

const blockPreview = (type) => {
  if (type === "radicated") return `<div id="diegoValoresRadicados" class="radicated-values"><p>Cargando información…</p></div>`;
  if (type === "table") return `<div class="block-table"><i></i><i></i><i></i><i></i></div>`;
  if (type === "donut") return `<div class="block-donut"><i></i><span>Sin datos</span></div>`;
  if (type === "funnel") return `<div class="block-funnel"><i></i><i></i><i></i><i></i></div>`;
  return `<div class="block-bars"><i></i><i></i><i></i><i></i><i></i></div>`;
};

const currencyFormatter = new Intl.NumberFormat("es-CO", {
  style: "currency",
  currency: "COP",
  maximumFractionDigits: 0
});

const loadDiegoRadicatedValues = async () => {
  const container = document.getElementById("diegoValoresRadicados");
  const year = document.getElementById("diegoYear").value;

  try {
    const response = await fetch(`/api/reports/fuerza-comercial-diego/valores-radicados?year=${encodeURIComponent(year)}`);
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    const data = await response.json();

    const valueKpi = document.querySelector(".diego-kpis article:nth-child(5) strong");
    valueKpi.textContent = currencyFormatter.format(data.totalAchieved ?? 0);

    if (!data.items?.length) {
      container.innerHTML = `<div class="empty-block"><strong>Sin valores radicados para ${data.year}</strong><span>Sincronice los negocios de Bitrix para cargar este indicador.</span></div>`;
      return;
    }

    container.innerHTML = `
      <div class="radicated-total"><span>Total alcanzado</span><strong>${currencyFormatter.format(data.totalAchieved)}</strong></div>
      <div class="radicated-table-wrap">
        <table class="radicated-table">
          <thead><tr><th>Mes</th><th>Asesor</th><th>Total alcanzado</th></tr></thead>
          <tbody>${data.items.map((item) => `
            <tr><td>${item.month}</td><td>${item.advisor}</td><td>${currencyFormatter.format(item.totalAchieved)}</td></tr>
          `).join("")}</tbody>
        </table>
      </div>`;
  } catch (error) {
    container.innerHTML = `<div class="empty-block error"><strong>No fue posible cargar los valores radicados</strong><span>${error.message}</span></div>`;
  }
};

const findDiegoBlock = (title) => [...document.querySelectorAll(".diego-block")]
  .find((block) => block.querySelector("h3")?.textContent === title);

const renderDataTable = (headers, rows) => `
  <div class="radicated-table-wrap synced-table-wrap">
    <table class="radicated-table synced-table">
      <thead><tr>${headers.map((header) => `<th>${header}</th>`).join("")}</tr></thead>
      <tbody>${rows.join("")}</tbody>
    </table>
  </div>`;

const replaceBlockPreview = (title, content, count) => {
  const block = findDiegoBlock(title);
  if (!block) return;
  block.querySelector(".diego-block-title em").textContent = `${count} registros`;
  const preview = block.querySelector(".block-table, .block-bars, .block-funnel, .block-donut");
  if (preview) preview.outerHTML = content;
};

const loadDiegoDashboardData = async () => {
  const year = document.getElementById("diegoYear").value;
  const response = await fetch(`/api/reports/fuerza-comercial-diego/dashboard?year=${encodeURIComponent(year)}`);
  if (!response.ok) throw new Error(`HTTP ${response.status}`);
  const data = await response.json();

  const totalNegotiations = data.advisors.reduce((sum, item) => sum + item.negotiations, 0);
  const totalCommercial = data.advisors.reduce((sum, item) => sum + item.commercialCases, 0);
  const totalRadicated = data.advisors.reduce((sum, item) => sum + item.radicatedCases, 0);
  document.querySelector(".diego-kpis article:nth-child(1) strong").textContent = formatNumber.format(totalNegotiations);
  document.querySelector(".diego-kpis article:nth-child(2) strong").textContent = formatNumber.format(totalCommercial);
  document.querySelector(".diego-kpis article:nth-child(3) strong").textContent = formatNumber.format(totalRadicated);
  document.querySelector(".diego-kpis article:nth-child(4) strong").textContent = totalCommercial
    ? `${((totalRadicated / totalCommercial) * 100).toFixed(1)}%`
    : "0%";

  replaceBlockPreview("Total de negociaciones por asesor", renderDataTable(
    ["Asesor", "Negociaciones", "Comerciales", "Radicados", "Valor"],
    data.advisors.map((item) => `<tr><td>${item.advisor}</td><td>${formatNumber.format(item.negotiations)}</td><td>${formatNumber.format(item.commercialCases)}</td><td>${formatNumber.format(item.radicatedCases)}</td><td>${currencyFormatter.format(item.totalValue)}</td></tr>`)
  ), data.advisors.length);

  const departmentRows = data.departments.map((item) => `<tr><td>${item.department}</td><td>${formatNumber.format(item.cases)}</td><td>${currencyFormatter.format(item.totalValue)}</td></tr>`);
  const departmentTable = renderDataTable(["Departamento", "Casos", "Valor alcanzado"], departmentRows);
  ["Valores radicados por coordinador", "Valores radicados por líder", "Detalle de coordinadores", "Detalle de radicaciones por líder"]
    .forEach((title) => replaceBlockPreview(title, departmentTable, data.departments.length));

  const pipelineBlocks = {
    rch_comercial: ["Etapas Comercial RCH", "Embudo RCH"],
    rch_operativa: ["Etapas Operativa RCH"],
    pnnc_comercial: ["Etapas Comercial PNNC", "Embudo Insolvencia"],
    pnnc_operativa: ["Etapas Operativa PNNC"]
  };

  Object.entries(pipelineBlocks).forEach(([pipeline, titles]) => {
    const items = data.stages.filter((item) => item.pipeline === pipeline);
    const content = renderDataTable(
      ["Etapa", "Casos", "Valor"],
      items.map((item) => `<tr><td>${item.stage}</td><td>${formatNumber.format(item.cases)}</td><td>${currencyFormatter.format(item.totalValue)}</td></tr>`)
    );
    titles.forEach((title) => replaceBlockPreview(title, content, items.length));
  });
};

const renderDiegoDashboard = () => {
  document.getElementById("diegoSections").innerHTML = diegoSections.map((section) => `
    <section id="${section.id}" class="diego-section">
      <header>
        <span>${section.icon}</span>
        <div><h2>${section.title}</h2><p>${section.description}</p></div>
      </header>
      <div class="diego-block-grid">
        ${section.blocks.map(([title, description, type]) => `
          <article class="diego-block diego-block-${type}">
            <div class="diego-block-title"><div><h3>${title}</h3><p>${description}</p></div><em>Sin datos</em></div>
            ${blockPreview(type)}
          </article>
        `).join("")}
      </div>
    </section>
  `).join("");
};

const setText = (id, value) => {
  document.getElementById(id).textContent = value;
};

const formatNumber = new Intl.NumberFormat("es-CO");

const loadSummary = async () => {
  const response = await fetch(`/api/data/sync-summary?pipeline=${reportId}`);
  const summary = await response.json();
  setText("summaryDeals", formatNumber.format(summary.dealsCount ?? 0));
  setText("summaryStages", formatNumber.format(summary.stagesCount ?? 0));
  setText("summaryUsers", formatNumber.format(summary.usersCount ?? 0));
  setText("summaryStatus", summary.lastSync?.status ?? "-");
  setText("summaryLastRun", summary.lastSync ? `${summary.lastSync.recordsWritten} escritos` : "Sin datos");
};

const loadDeals = async () => {
  const response = await fetch(`/api/data/deals?pipeline=${reportId}`);
  const deals = await response.json();

  document.getElementById("dealRows").innerHTML = deals.map((deal) => `
    <tr>
      <td>${deal.bitrixId}</td>
      <td><strong>${deal.title}</strong></td>
      <td>${deal.stageName ?? deal.stageId ?? ""}</td>
      <td>${deal.responsibleName ?? ""}</td>
      <td>${deal.opportunity ?? ""}</td>
      <td>${deal.currencyId ?? ""}</td>
    </tr>
  `).join("");

  const byStage = new Map();
  const byOwner = new Map();

  deals.forEach((deal) => {
    const stage = deal.stageName ?? deal.stageId ?? "Sin etapa";
    const owner = deal.responsibleName ?? "Sin responsable";
    byStage.set(stage, (byStage.get(stage) ?? 0) + 1);
    byOwner.set(owner, (byOwner.get(owner) ?? 0) + 1);
  });

  const maxStage = Math.max(...byStage.values(), 1);
  document.getElementById("stageBars").innerHTML = [...byStage.entries()]
    .slice(0, 8)
    .map(([stage, count]) => `
      <div class="stage-row">
        <span>${stage}</span>
        <div><i style="width:${Math.max(8, (count / maxStage) * 100)}%"></i></div>
        <b>${count}</b>
      </div>
    `).join("");

  document.getElementById("ownerList").innerHTML = [...byOwner.entries()]
    .sort((a, b) => b[1] - a[1])
    .slice(0, 8)
    .map(([owner, count]) => `
      <div class="owner-row">
        <span>${owner}</span>
        <b>${count}</b>
      </div>
    `).join("");
};

const load = async () => {
  const current = metadata[reportId] ?? metadata.rch_comercial;
  setText("reportName", current.name);
  setText("reportTitle", current.name);
  setText("reportDescription", current.description);
  setText("reportArea", current.area);
  if (reportId === "fuerza_comercial_diego") {
    document.getElementById("standardSummary").hidden = true;
    document.getElementById("standardVisuals").hidden = true;
    document.getElementById("detalle").hidden = true;
    document.getElementById("diegoDashboard").hidden = false;
    document.querySelector(".menu").innerHTML = `
      <p>GENERAL</p>
      <a href="/"><span>🏠</span>Inicio</a>
      <a href="/informes.html"><span>📊</span>Informes</a>
      <p>BLOQUES</p>
      <a class="active" href="#radicacion"><span>◎</span>Radicación</a>
      <a href="#carteras"><span>$</span>Estado de carteras</a>
      <a href="#embudos"><span>▽</span>Embudos</a>
      <a href="#etapas"><span>≡</span>Etapas</a>`;
    renderDiegoDashboard();
    document.getElementById("diegoYear").addEventListener("change", async () => {
      await Promise.all([loadDiegoRadicatedValues(), loadDiegoDashboardData()]);
    });
    await Promise.all([loadDiegoRadicatedValues(), loadDiegoDashboardData()]);
    return;
  }
  await loadSummary();
  await loadDeals();
};

load();
