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

    const months = [...new Set(data.items.map((item) => item.month))]
      .sort((left, right) => Number.parseInt(left, 10) - Number.parseInt(right, 10));
    const advisorValues = new Map();
    data.items.forEach((item) => {
      if (!advisorValues.has(item.advisor)) advisorValues.set(item.advisor, new Map());
      const monthlyValues = advisorValues.get(item.advisor);
      monthlyValues.set(item.month, (monthlyValues.get(item.month) ?? 0) + item.totalAchieved);
    });
    const advisors = [...advisorValues.keys()].sort((left, right) => left.localeCompare(right, "es"));

    container.innerHTML = `
      <div class="radicated-total"><span>Total alcanzado</span><strong>${currencyFormatter.format(data.totalAchieved)}</strong></div>
      <div class="radicated-table-wrap radicated-matrix-wrap">
        <table class="radicated-table radicated-matrix">
          <thead>
            <tr><th rowspan="2">Asesor</th><th colspan="${months.length}">Total alcanzado</th></tr>
            <tr>${months.map((month) => `<th data-month="${month.slice(0, 2)}">${month}</th>`).join("")}</tr>
          </thead>
          <tbody>${advisors.map((advisor) => `
            <tr><td>${advisor}</td>${months.map((month) => {
              const value = advisorValues.get(advisor).get(month);
              return `<td data-month="${month.slice(0, 2)}">${value ? formatNumber.format(value) : ""}</td>`;
            }).join("")}</tr>
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

const loadDiegoPortfolioCollections = async () => {
  const year = document.getElementById("diegoYear").value;
  const response = await fetch(`/api/reports/fuerza-comercial-diego/cartera-recaudada?year=${encodeURIComponent(year)}`);
  if (!response.ok) throw new Error(`HTTP ${response.status}`);
  const data = await response.json();
  document.querySelector(".diego-kpis article:nth-child(6) strong").textContent = currencyFormatter.format(data.totalCollected ?? 0);
  const rows = data.items.map((item) => `<tr><td>${item.month}</td><td>${item.commercialLine}</td><td>${currencyFormatter.format(item.collected)}</td></tr>`);
  const content = rows.length
    ? renderDataTable(["Mes", "Línea comercial", "Recaudo"], rows)
    : `<div class="empty-block"><strong>Sin recaudos para ${data.year}</strong><span>Las pipelines de cartera aún se están sincronizando.</span></div>`;
  replaceBlockPreview("Cartera recaudada", content, data.items.length);
};

const loadDiegoLeadershipAndCommissions = async () => {
  const year = document.getElementById("diegoYear").value;
  const response = await fetch(`/api/reports/fuerza-comercial-diego/liderazgo-comisiones?year=${encodeURIComponent(year)}`);
  if (!response.ok) throw new Error(`HTTP ${response.status}`);
  const data = await response.json();

  const leaderRows = data.leadership.map((item) => `<tr><td>${item.month}</td><td>${item.leader}</td><td>${currencyFormatter.format(item.totalAchieved)}</td></tr>`);
  const coordinatorRows = data.leadership.map((item) => `<tr><td>${item.month}</td><td>${item.coordinator}</td><td>${currencyFormatter.format(item.totalAchieved)}</td></tr>`);
  const commissionRows = data.commissions.map((item) => `<tr><td>${item.month}</td><td>${item.advisor}</td><td>${currencyFormatter.format(item.total)}</td></tr>`);

  replaceBlockPreview("Valores radicados por líder", renderDataTable(["Mes", "Líder", "Total alcanzado"], leaderRows), leaderRows.length);
  replaceBlockPreview("Valores radicados por coordinador", renderDataTable(["Mes", "Coordinador", "Total alcanzado"], coordinatorRows), coordinatorRows.length);
  replaceBlockPreview("Comisiones por asesor", commissionRows.length
    ? renderDataTable(["Mes", "Asesor", "Comisión"], commissionRows)
    : `<div class="empty-block"><strong>Sin comisiones para ${data.year}</strong><span>La pipeline Cuentas de Cobro no contiene registros pagados para este periodo.</span></div>`, commissionRows.length);
};

const normalizeFilterText = (value) => value.trim().toLocaleLowerCase("es-CO");

const collectColumnValues = (headerName) => {
  const values = new Set();
  document.querySelectorAll(".diego-block table").forEach((table) => {
    const headers = [...table.querySelectorAll("thead th")].map((header) => header.textContent.trim());
    const index = headers.indexOf(headerName);
    if (index < 0) return;
    table.querySelectorAll("tbody tr").forEach((row) => {
      const value = row.children[index]?.textContent.trim();
      if (value && !value.startsWith("Sin ")) values.add(value);
    });
  });
  return [...values].sort((left, right) => left.localeCompare(right, "es"));
};

const fillFilterOptions = (id, values) => {
  const select = document.getElementById(id);
  const previous = select.value;
  select.replaceChildren();
  const allOption = document.createElement("option");
  allOption.value = "all";
  allOption.textContent = "Todos";
  select.append(allOption);
  values.forEach((value) => {
    const option = document.createElement("option");
    option.value = value;
    option.textContent = value;
    select.append(option);
  });
  if ([...select.options].some((option) => option.value === previous)) select.value = previous;
};

const applyDiegoFilters = () => {
  const filters = {
    Mes: document.getElementById("diegoMonth").value,
    "Línea comercial": document.getElementById("diegoLine").value,
    Asesor: document.getElementById("diegoAdvisor").value,
    Líder: document.getElementById("diegoLeader").value,
    Coordinador: document.getElementById("diegoCoordinator").value
  };
  const selectedLine = document.getElementById("diegoLine").value;
  const selectedMonth = document.getElementById("diegoMonth").value;

  document.querySelectorAll(".diego-block").forEach((block) => {
    const title = normalizeFilterText(block.querySelector("h3")?.textContent ?? "");
    const belongsToRch = title.includes("rch");
    const belongsToInsolvency = title.includes("pnnc") || title.includes("insolvencia");
    block.hidden = (selectedLine === "rch" && belongsToInsolvency)
      || (selectedLine === "insolvencia" && belongsToRch);

    const table = block.querySelector("table");
    if (!table) return;
    if (table.classList.contains("radicated-matrix")) {
      table.querySelectorAll("[data-month]").forEach((cell) => {
        cell.hidden = selectedMonth !== "all" && cell.dataset.month !== selectedMonth;
      });
    }
    const headers = [...table.querySelectorAll("thead th")].map((header) => header.textContent.trim());
    let visibleRows = 0;

    table.querySelectorAll("tbody tr").forEach((row) => {
      const matches = Object.entries(filters).every(([headerName, selected]) => {
        if (selected === "all") return true;
        const index = headers.indexOf(headerName);
        if (index < 0) return true;
        const cellValue = row.children[index]?.textContent.trim() ?? "";
        if (headerName === "Mes") return cellValue.startsWith(selected);
        if (headerName === "Línea comercial") return normalizeFilterText(cellValue).includes(selected);
        return normalizeFilterText(cellValue) === normalizeFilterText(selected);
      });
      row.hidden = !matches;
      if (matches) visibleRows += 1;
    });

    const badge = block.querySelector(".diego-block-title em");
    if (badge) badge.textContent = `${visibleRows} registros`;
  });
};

const setupDiegoFilters = () => {
  fillFilterOptions("diegoAdvisor", collectColumnValues("Asesor"));
  fillFilterOptions("diegoLeader", collectColumnValues("Líder"));
  fillFilterOptions("diegoCoordinator", collectColumnValues("Coordinador"));
  ["diegoMonth", "diegoLine", "diegoAdvisor", "diegoLeader", "diegoCoordinator"].forEach((id) => {
    const select = document.getElementById(id);
    if (select.dataset.bound === "true") return;
    select.addEventListener("change", applyDiegoFilters);
    select.dataset.bound = "true";
  });
  applyDiegoFilters();
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

  await loadStageDistribution();
  await loadResponsibleDistribution();
};

const loadStageDistribution = async () => {
  const response = await fetch(`/api/data/stage-distribution?pipeline=${reportId}`);
  const rows = await response.json();
  const maxStage = Math.max(...rows.map((row) => row.dealsCount), 1);

  document.getElementById("stageBars").innerHTML = rows
    .map((row) => `
      <div class="stage-row">
        <span>${row.stageName}</span>
        <div><i style="width:${Math.max(8, (row.dealsCount / maxStage) * 100)}%"></i></div>
        <b>${row.dealsCount}</b>
      </div>
    `).join("");
};

const loadResponsibleDistribution = async () => {
  const response = await fetch(`/api/data/responsible-distribution?pipeline=${reportId}`);
  const rows = await response.json();

  document.getElementById("ownerList").innerHTML = rows
    .map((row) => `
      <div class="owner-row">
        <span>${row.responsibleName}</span>
        <b>${row.dealsCount}</b>
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
    document.querySelector(".compact-hero").hidden = true;
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
      await Promise.all([loadDiegoRadicatedValues(), loadDiegoDashboardData(), loadDiegoPortfolioCollections(), loadDiegoLeadershipAndCommissions()]);
      setupDiegoFilters();
    });
    await Promise.all([loadDiegoRadicatedValues(), loadDiegoDashboardData(), loadDiegoPortfolioCollections(), loadDiegoLeadershipAndCommissions()]);
    setupDiegoFilters();
    return;
  }
  await loadSummary();
  await loadDeals();
};

const updateReportView = async () => {
  setText("reportStatus", "Leyendo");
  try {
    if (reportId === "fuerza_comercial_diego") {
      await Promise.all([loadDiegoRadicatedValues(), loadDiegoDashboardData()]);
    } else {
      await loadSummary();
      await loadDeals();
    }

    setText("reportStatus", "OK");
  } catch {
    setText("reportStatus", "Error");
  }
};

document.getElementById("refreshReportButton").addEventListener("click", updateReportView);

load();
