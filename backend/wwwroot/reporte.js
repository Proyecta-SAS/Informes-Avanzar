const params = new URLSearchParams(window.location.search);
const reportId = params.get("id") ?? "rch_comercial";

const metadata = {
  rch_comercial: { name: "RCH Comercial", area: "Comercial", description: "Seguimiento comercial de negociaciones RCH." },
  rch_operativa: { name: "RCH Operativa", area: "Operaciones", description: "Seguimiento operativo de negociaciones RCH." },
  pnnc_comercial: { name: "PNNC Comercial", area: "Comercial", description: "Dashboard comercial PNNC." },
  pnnc_operativa: { name: "PNNC Operativa", area: "Operaciones", description: "Dashboard operativo PNNC." },
  informe_general_comercial: {
    name: "Informe General Comercial",
    area: "Comercial",
    description: "Vista consolidada de radicación, negociaciones, comisiones, cartera, embudos y etapas comerciales."
  },
  fuerza_comercial_diego: {
    name: "Fuerza Comercial",
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
      ["Detalle de radicaciones por líder", "Radicación y cumplimiento desagregados por líder.", "table"]
    ]
  },
  {
    id: "comisiones",
    icon: "$",
    title: "Cobro comisiones asesores",
    description: "Liquidación mensual y consolidado de comisiones pagadas a la fuerza comercial.",
    blocks: [
      ["Comisiones por asesor", "Valores mensuales y total acumulado por asesor durante 2026.", "commissions"]
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
      ["Etapas Operativa PNNC", "Validación y estado de la documentación comercial.", "bars"],
      ["Posible cierre PNC", "Monto y número de casos PNNC que avanzan hacia posible cierre.", "table"]
    ]
  }
];

const generalBlockCodes = {
  "Valores radicados por asesor": "radicated_values",
  "Total de negociaciones por asesor": "advisor_negotiations",
  "Valores radicados por coordinador": "coordinator_values",
  "Valores radicados por líder": "leader_values",
  "Detalle de coordinadores": "coordinator_detail",
  "Detalle de radicaciones por líder": "leader_detail",
  "Comisiones por asesor": "advisor_commissions",
  "Estado de cartera 2025": "portfolio_state",
  "Cartera recaudada": "portfolio_collected",
  "Embudo Insolvencia": "funnel_insolvency",
  "Embudo RCH": "funnel_rch",
  "Etapas Comercial RCH": "stages_rch_commercial",
  "Etapas Operativa RCH": "stages_rch_operativa",
  "Etapas Comercial PNNC": "stages_pnnc_commercial",
  "Etapas Operativa PNNC": "stages_pnnc_operativa",
  "Posible cierre PNC": "possible_close_pnnc"
};
let generalBlockAccess = { configured: false, codes: new Set() };

const blockPreview = (type) => {
  if (type === "radicated") return `<div id="diegoValoresRadicados" class="radicated-values"><p>Cargando información…</p></div>`;
  if (type === "commissions") return `<div class="block-table commission-placeholder"><i></i><i></i><i></i><i></i></div>`;
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

const portfolioCollectionGoals = {
  "01": 1840070000,
  "02": 1840070000,
  "03": 1620000000,
  "04": 1860080000,
  "05": 1909080000,
  "06": 1909080000,
  "07": 2091000000,
  "08": 1959000000,
  "09": 524700000
};

const spanishMonthLabels = {
  "01": "01 ENE", "02": "02 FEB", "03": "03 MAR", "04": "04 ABR",
  "05": "05 MAY", "06": "06 JUN", "07": "07 JUL", "08": "08 AGO",
  "09": "09 SEP", "10": "10 OCT", "11": "11 NOV", "12": "12 DIC"
};

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

const findDiegoBlock = (title) => document.querySelector(`.diego-block[data-block-title="${CSS.escape(title)}"]`);

const generalCommercialLabels = {
  "Valores radicados por asesor": "(COM) Valores Radicados 2026",
  "Total de negociaciones por asesor": "(COM) Total Negociaciones por Asesor",
  "Valores radicados por coordinador": "(COM) Valores Radicados Coordinadores 2026",
  "Valores radicados por líder": "(COM) Valores Radicados Lideres 2026",
  "Detalle de coordinadores": "Detalle Coordinadores 2026",
  "Detalle de radicaciones por líder": "(COM) Valores Radicaciones Lideres 2026",
  "Comisiones por asesor": "(COM) Comisiones Asesor 2026",
  "Estado de cartera 2025": "(COM) Estado de cartera 2025",
  "Cartera recaudada": "(COM) Cartera Recaudada",
  "Embudo Insolvencia": "(COM) Embudo Insolvencia",
  "Embudo RCH": "(COM) Embudo RCH",
  "Etapas Comercial RCH": "(COM) ETAPAS COMERCIAL RCH",
  "Etapas Operativa RCH": "(COM) ETAPAS OPERATIVA RCH",
  "Etapas Comercial PNNC": "(COM) ETAPAS COMERCIAL PNNC",
  "Etapas Operativa PNNC": "(COM) ETAPAS OPERATIVA PNNC"
};

const applyGeneralCommercialLabels = () => {
  Object.entries(generalCommercialLabels).forEach(([source, label]) => {
    const heading = findDiegoBlock(source)?.querySelector("h3");
    if (heading) heading.textContent = label;
  });
};

const renderDataTable = (headers, rows, className = "") => `
  <div class="radicated-table-wrap synced-table-wrap">
    <table class="radicated-table synced-table ${className}">
      <thead><tr>${headers.map((header) => `<th>${header}</th>`).join("")}</tr></thead>
      <tbody>${rows.join("")}</tbody>
    </table>
  </div>`;

const renderPipelineTable = (items, mode) => {
  const sortedItems = [...items].sort((left, right) => right.cases - left.cases);
  const maxCases = Math.max(...sortedItems.map((item) => item.cases), 1);
  const isFunnel = mode === "funnel";
  const headers = isFunnel
    ? ["Etapa", "COUNT(*)"]
    : mode === "commercial"
      ? ["ETAPA COMERCIAL RCH", "# CASOS COMERCIAL", "$ VALOR COMERCIAL"]
      : mode === "operative"
        ? ["ETAPA OPERATIVA RCH", "# CASOS OPERATIVA", "$ VALOR COMERCIAL"]
        : mode === "pnnc-commercial"
          ? ["ETAPA COMERCIAL PNNC", "# CASOS COMERCIAL PNNC", "$ VALOR COMERCIAL"]
          : ["ETAPA OPERATIVA PNNC", "# CASOS OPERATIVA PNNC", "$ VALOR OPERATIVA"];
  const rows = sortedItems.map((item) => {
    const intensity = (item.cases / maxCases).toFixed(3);
    if (isFunnel) {
      return `<tr><td>${item.stage}</td><td class="pipeline-heat" style="--heat:${intensity}">${formatNumber.format(item.cases)}</td></tr>`;
    }
    return `<tr><td>${item.stage}</td><td>${formatNumber.format(item.cases)}</td><td>${formatNumber.format(item.totalValue)}</td></tr>`;
  });
  return renderDataTable(headers, rows, `pipeline-table pipeline-table-${mode}`);
};

const renderMonthlyMatrix = (groupLabel, items, groupField) => {
  const months = [...new Set(items.map((item) => item.month))]
    .sort((left, right) => Number.parseInt(left, 10) - Number.parseInt(right, 10));
  const groupedValues = new Map();
  items.forEach((item) => {
    const group = item[groupField] || `Sin ${groupLabel.toLocaleLowerCase("es-CO")}`;
    if (!groupedValues.has(group)) groupedValues.set(group, new Map());
    const monthlyValues = groupedValues.get(group);
    monthlyValues.set(item.month, (monthlyValues.get(item.month) ?? 0) + item.totalAchieved);
  });
  const groups = [...groupedValues.keys()]
    .sort((left, right) => left.localeCompare(right, "es", { sensitivity: "base" }));

  return `
    <div class="radicated-table-wrap leadership-matrix-wrap">
      <table class="radicated-table monthly-matrix leadership-matrix">
        <thead>
          <tr><th rowspan="2">${groupLabel}</th><th colspan="${months.length}">Total alcanzado</th></tr>
          <tr>${months.map((month) => `<th data-month="${month.slice(0, 2)}">${month}</th>`).join("")}</tr>
        </thead>
        <tbody>${groups.map((group) => `
          <tr><td>${group}</td>${months.map((month) => {
            const value = groupedValues.get(group).get(month);
            return `<td data-month="${month.slice(0, 2)}">${value ? formatNumber.format(value) : ""}</td>`;
          }).join("")}</tr>
        `).join("")}</tbody>
      </table>
    </div>`;
};

const monthlyLeaderGoal = (month) => Number.parseInt(month, 10) >= 7 ? 70000000 : 60000000;

const renderPerformanceTable = (items, groupField, coordinatorMode = false) => {
  const grouped = new Map();
  items.forEach((item) => {
    const group = item[groupField] || `Sin ${groupField}`;
    const key = `${item.month}|${group}`;
    if (!grouped.has(key)) grouped.set(key, { month: item.month, group, total: 0, leaders: new Set() });
    const row = grouped.get(key);
    row.total += item.totalAchieved;
    if (item.leader) row.leaders.add(item.leader);
  });

  const rows = [...grouped.values()]
    .map((row) => {
      const goal = monthlyLeaderGoal(row.month) * (coordinatorMode ? Math.max(1, row.leaders.size) : 1);
      return { ...row, goal, compliance: goal ? (row.total / goal) * 100 : 0 };
    })
    .sort((left, right) => right.total - left.total);

  return {
    count: rows.length,
    html: `<div class="radicated-table-wrap performance-table-wrap">
      <table class="radicated-table synced-table performance-table">
        <thead><tr><th>Mes</th><th>Meta</th><th>Valor alcanzado</th><th>% de cumplimiento</th></tr></thead>
        <tbody>${rows.map((row) => `<tr data-group="${row.group}"><td>${row.month}</td><td>${formatNumber.format(row.goal)}</td><td>${formatNumber.format(row.total)}</td><td>${row.compliance.toLocaleString("es-CO", { minimumFractionDigits: 1, maximumFractionDigits: 1 })}%</td></tr>`).join("")}</tbody>
      </table>
    </div>`
  };
};

const renderCommissionMatrix = (items) => {
  const months = [...new Set(items.map((item) => item.month))]
    .sort((left, right) => Number.parseInt(left, 10) - Number.parseInt(right, 10));
  const advisorValues = new Map();
  items.forEach((item) => {
    if (!advisorValues.has(item.advisor)) advisorValues.set(item.advisor, new Map());
    const values = advisorValues.get(item.advisor);
    values.set(item.month, (values.get(item.month) ?? 0) + item.total);
  });
  const advisors = [...advisorValues.keys()].sort((left, right) => left.localeCompare(right, "es", { sensitivity: "base" }));
  const monthTotals = new Map(months.map((month) => [month, items.filter((item) => item.month === month).reduce((sum, item) => sum + item.total, 0)]));
  const grandTotal = [...monthTotals.values()].reduce((sum, value) => sum + value, 0);

  return `<div class="radicated-table-wrap commission-matrix-wrap">
    <table class="radicated-table monthly-matrix commission-matrix">
      <thead>
        <tr><th rowspan="2">Asesor</th><th colspan="${months.length}">Total</th><th rowspan="2">Total (Sum)</th></tr>
        <tr>${months.map((month) => `<th data-month="${month.slice(0, 2)}">${month}</th>`).join("")}</tr>
      </thead>
      <tbody>${advisors.map((advisor) => {
        const values = advisorValues.get(advisor);
        const advisorTotal = [...values.values()].reduce((sum, value) => sum + value, 0);
        return `<tr><td>${advisor}</td>${months.map((month) => `<td data-month="${month.slice(0, 2)}">${values.get(month) ? formatNumber.format(values.get(month)) : ""}</td>`).join("")}<td>${formatNumber.format(advisorTotal)}</td></tr>`;
      }).join("")}</tbody>
      <tfoot><tr><th>Total (Sum)</th>${months.map((month) => `<td data-month="${month.slice(0, 2)}">${formatNumber.format(monthTotals.get(month))}</td>`).join("")}<td>${formatNumber.format(grandTotal)}</td></tr></tfoot>
    </table>
  </div>`;
};

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
    ["Asesor", "Total de negociaciones", "Estudios", "Estudios sobre total", "Radicados", "Tasa de cierre"],
    [...data.advisors]
      .sort((left, right) => left.advisor.localeCompare(right.advisor, "es", { sensitivity: "base" }))
      .map((item) => {
        const studiesRate = item.negotiations ? `${((item.commercialCases / item.negotiations) * 100).toFixed(1)}%` : "0.0%";
        const closingRate = item.commercialCases ? `${((item.radicatedCases / item.commercialCases) * 100).toFixed(1)}%` : "N/A";
        return `<tr><td>${item.advisor}</td><td>${formatNumber.format(item.negotiations)}</td><td>${formatNumber.format(item.commercialCases)}</td><td>${studiesRate}</td><td>${formatNumber.format(item.radicatedCases)}</td><td>${closingRate}</td></tr>`;
      }),
    "advisor-negotiations-table"
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
    titles.forEach((title) => {
      const mode = title.startsWith("Embudo")
        ? "funnel"
        : title === "Etapas Comercial RCH"
          ? "commercial"
          : title === "Etapas Operativa RCH"
            ? "operative"
            : title === "Etapas Comercial PNNC" ? "pnnc-commercial" : "pnnc-operative";
      replaceBlockPreview(title, renderPipelineTable(items, mode), items.length);
    });
  });

  const possibleCloseRows = data.possibleClosePnnc ?? [];
  const possibleCloseAmount = possibleCloseRows.reduce((sum, item) => sum + Number(item.amount ?? 0), 0);
  const possibleCloseCases = possibleCloseRows.reduce((sum, item) => sum + Number(item.cases ?? 0), 0);
  const possibleCloseTable = `<div class="radicated-table-wrap possible-close-wrap"><table class="radicated-table synced-table possible-close-table"><thead><tr><th>Etapa</th><th>Monto</th><th>Casos</th></tr></thead><tbody>${possibleCloseRows.map((item) => `<tr><td>${item.stage}</td><td>${formatNumber.format(item.amount)}</td><td>${formatNumber.format(item.cases)}</td></tr>`).join("")}</tbody><tfoot><tr><th>Total (Sum)</th><td>${formatNumber.format(possibleCloseAmount)}</td><td>${formatNumber.format(possibleCloseCases)}</td></tr></tfoot></table></div>`;
  replaceBlockPreview("Posible cierre PNC", possibleCloseRows.length ? possibleCloseTable : `<div class="empty-block"><strong>Sin casos de posible cierre PNNC</strong><span>No hay negocios en las etapas configuradas.</span></div>`, possibleCloseRows.length);
};

const loadDiegoPortfolioCollections = async () => {
  const year = document.getElementById("diegoYear").value;
  const response = await fetch(`/api/reports/fuerza-comercial-diego/cartera-recaudada?year=${encodeURIComponent(year)}`);
  if (!response.ok) throw new Error(`HTTP ${response.status}`);
  const data = await response.json();
  document.querySelector(".diego-kpis article:nth-child(6) strong").textContent = currencyFormatter.format(data.totalCollected ?? 0);
  const portfolioRows = data.portfolio.map((item) => `<tr><td>${item.advisor}</td><td><span class="portfolio-line ${normalizeFilterText(item.commercialLine)}">${item.commercialLine}</span></td><td>${formatNumber.format(item.receivable)}</td><td>${formatNumber.format(item.withNovelty)}</td><td>${formatNumber.format(item.successful)}</td></tr>`);
  const portfolioContent = portfolioRows.length
    ? renderDataTable(["Asesor", "Línea", "Valor cartera por cobrar", "Valor cartera con novedad", "Valor cartera exitosa"], portfolioRows, "portfolio-state-table")
    : `<div class="empty-block"><strong>Sin cartera disponible</strong><span>Sincronice las pipelines RCH Cartera e Insolvencia Cartera.</span></div>`;
  replaceBlockPreview("Estado de cartera 2025", portfolioContent, data.portfolio.length);
  const collectionsByMonth = new Map();
  data.items.forEach((item) => {
    const monthKey = item.month.slice(0, 2);
    collectionsByMonth.set(monthKey, (collectionsByMonth.get(monthKey) ?? 0) + item.collected);
  });
  const rows = [...collectionsByMonth.entries()]
    .map(([month, collected]) => ({ month, collected, goal: portfolioCollectionGoals[month] ?? 0 }))
    .sort((left, right) => right.goal - left.goal)
    .map((item) => `<tr><td>${spanishMonthLabels[item.month]}</td><td>${formatNumber.format(item.goal)}</td><td>${formatNumber.format(item.collected)}</td></tr>`);
  const content = rows.length
    ? renderDataTable(["Mes", "Meta", "Recaudo"], rows, "portfolio-collection-table")
    : `<div class="empty-block"><strong>Sin recaudos para ${data.year}</strong><span>Las pipelines de cartera aún se están sincronizando.</span></div>`;
  replaceBlockPreview("Cartera recaudada", content, data.items.length);
};

const loadDiegoLeadershipAndCommissions = async () => {
  const year = document.getElementById("diegoYear").value;
  const response = await fetch(`/api/reports/fuerza-comercial-diego/liderazgo-comisiones?year=${encodeURIComponent(year)}`);
  if (!response.ok) throw new Error(`HTTP ${response.status}`);
  const data = await response.json();

  const leaderCount = new Set(data.leadership.map((item) => item.leader).filter(Boolean)).size;
  const coordinatorCount = new Set(data.leadership.map((item) => item.coordinator).filter(Boolean)).size;
  replaceBlockPreview("Valores radicados por líder", renderMonthlyMatrix("Líder", data.leadership, "leader"), leaderCount);
  replaceBlockPreview("Valores radicados por coordinador", renderMonthlyMatrix("Coordinador", data.leadership, "coordinator"), coordinatorCount);
  const coordinatorPerformance = renderPerformanceTable(data.leadership, "coordinator", true);
  const leaderPerformance = renderPerformanceTable(data.leadership, "leader");
  replaceBlockPreview("Detalle de coordinadores", coordinatorPerformance.html, coordinatorPerformance.count);
  replaceBlockPreview("Detalle de radicaciones por líder", leaderPerformance.html, leaderPerformance.count);
  replaceBlockPreview("Comisiones por asesor", data.commissions.length
    ? renderCommissionMatrix(data.commissions)
    : `<div class="empty-block"><strong>Sin comisiones para ${data.year}</strong><span>La pipeline Cuentas de Cobro no contiene registros pagados para este periodo.</span></div>`, data.commissions.length);
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
  const selectedPendingLeader = document.getElementById("diegoPendingLeader").value;

  document.querySelectorAll(".diego-block").forEach((block) => {
    const title = normalizeFilterText(block.querySelector("h3")?.textContent ?? "");
    const belongsToRch = title.includes("rch");
    const belongsToInsolvency = title.includes("pnnc") || title.includes("insolvencia");
    block.hidden = (selectedLine === "rch" && belongsToInsolvency)
      || (selectedLine === "insolvencia" && belongsToRch);

    const table = block.querySelector("table");
    if (!table) return;
    if (table.classList.contains("radicated-matrix") || table.classList.contains("monthly-matrix")) {
      table.querySelectorAll("[data-month]").forEach((cell) => {
        cell.hidden = selectedMonth !== "all" && cell.dataset.month !== selectedMonth;
      });
    }
    const headers = [...table.querySelectorAll("thead th")].map((header) => header.textContent.trim());
    let visibleRows = 0;

    table.querySelectorAll("tbody tr").forEach((row) => {
      const matches = Object.entries(filters).every(([headerName, selected]) => {
        if (selected === "all") return true;
        if (table.classList.contains("performance-table") && row.dataset.group) {
          if (headerName === "Coordinador" && title.includes("coordinadores")) return normalizeFilterText(row.dataset.group) === normalizeFilterText(selected);
          if (headerName === "Líder" && title.includes("líder")) return normalizeFilterText(row.dataset.group) === normalizeFilterText(selected);
        }
        const index = headers.indexOf(headerName);
        if (index < 0) return true;
        const cellValue = row.children[index]?.textContent.trim() ?? "";
        if (headerName === "Mes") return cellValue.startsWith(selected);
        if (headerName === "Línea comercial") return normalizeFilterText(cellValue).includes(selected);
        return normalizeFilterText(cellValue) === normalizeFilterText(selected);
      });
      const stageIndex = headers.findIndex((header) => normalizeFilterText(header).startsWith("etapa"));
      const stageValue = stageIndex >= 0 ? normalizeFilterText(row.children[stageIndex]?.textContent ?? "") : "";
      const isPendingLeader = stageValue.includes("lider") || stageValue.includes("líder");
      const matchesPendingLeader = selectedPendingLeader === "all"
        || stageIndex < 0
        || (selectedPendingLeader === "pending" ? isPendingLeader : !isPendingLeader);
      row.hidden = !(matches && matchesPendingLeader);
      if (matches && matchesPendingLeader) visibleRows += 1;
    });

    const badge = block.querySelector(".diego-block-title em");
    if (badge) badge.textContent = `${visibleRows} registros`;
  });
};

const setupDiegoFilters = () => {
  fillFilterOptions("diegoAdvisor", collectColumnValues("Asesor"));
  fillFilterOptions("diegoLeader", collectColumnValues("Líder"));
  fillFilterOptions("diegoCoordinator", collectColumnValues("Coordinador"));
  ["diegoMonth", "diegoLine", "diegoAdvisor", "diegoLeader", "diegoCoordinator", "diegoPendingLeader"].forEach((id) => {
    const select = document.getElementById(id);
    if (select.dataset.bound === "true") return;
    select.addEventListener("change", applyDiegoFilters);
    select.dataset.bound = "true";
  });
  applyDiegoFilters();
};

const renderDiegoDashboard = () => {
  const sections = diegoSections.map((section) => {
    const reportBlocks = section.blocks.filter(([title]) => reportId === "informe_general_comercial" || title !== "Posible cierre PNC");
    return { ...section, blocks: reportId === "informe_general_comercial" && generalBlockAccess.configured
      ? reportBlocks.filter(([title]) => generalBlockAccess.codes.has(generalBlockCodes[title]))
      : reportBlocks };
  }).filter((section) => section.blocks.length);
  document.getElementById("diegoSections").innerHTML = sections.map((section) => `
    <section id="${section.id}" class="diego-section">
      <header>
        <span>${section.icon}</span>
        <div><h2>${section.title}</h2><p>${section.description}</p></div>
      </header>
      <div class="diego-block-grid">
        ${section.blocks.map(([title, description, type]) => `
          <article data-block-title="${title}" data-block-code="${generalBlockCodes[title]}" class="diego-block diego-block-${type}${["Total de negociaciones por asesor", "Cartera recaudada"].includes(title) ? " diego-block-wide-table" : ""}">
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
  if (["fuerza_comercial_diego", "informe_general_comercial"].includes(reportId)) {
    const isGeneralCommercial = reportId === "informe_general_comercial";
    if (isGeneralCommercial) {
      const session = await fetch("/api/auth/me").then((response) => response.json());
      generalBlockAccess = { configured: Boolean(session.generalCommercialBlocksConfigured), codes: new Set(session.generalCommercialBlockCodes ?? []) };
    }
    document.body.classList.toggle("general-commercial-report", isGeneralCommercial);
    document.querySelector(".compact-hero").hidden = true;
    document.getElementById("standardSummary").hidden = true;
    document.getElementById("standardVisuals").hidden = true;
    document.getElementById("detalle").hidden = true;
    document.getElementById("diegoDashboard").hidden = false;
    if (isGeneralCommercial) {
      document.title = "Informe General Comercial | Avanzar";
      document.querySelector(".diego-overview-kicker").textContent = "Panel general · Información consolidada desde Bitrix";
      document.querySelector(".diego-overview h2").textContent = "Informe general del área comercial";
      document.querySelector(".diego-overview p").textContent = "Consulta radicación, negociaciones, comisiones, cartera, embudos y etapas sincronizadas desde Bitrix.";
      document.getElementById("diegoYearFilter").hidden = true;
      document.getElementById("diegoMonthFilter").hidden = true;
      document.getElementById("pendingLeaderFilter").hidden = false;
    }
    renderDiegoDashboard();
    document.getElementById("diegoYear").addEventListener("change", async () => {
      await Promise.all([loadDiegoRadicatedValues(), loadDiegoDashboardData(), loadDiegoPortfolioCollections(), loadDiegoLeadershipAndCommissions()]);
      setupDiegoFilters();
    });
    await Promise.all([loadDiegoRadicatedValues(), loadDiegoDashboardData(), loadDiegoPortfolioCollections(), loadDiegoLeadershipAndCommissions()]);
    if (isGeneralCommercial) applyGeneralCommercialLabels();
    setupDiegoFilters();
    return;
  }
  await loadSummary();
  await loadDeals();
};

const updateReportView = async () => {
  setText("reportStatus", "Leyendo");
  try {
    if (["fuerza_comercial_diego", "informe_general_comercial"].includes(reportId)) {
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
