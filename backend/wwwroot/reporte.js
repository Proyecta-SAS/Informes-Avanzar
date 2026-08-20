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
    description: "Vista consolidada de radicación, negociaciones, comisiones, cartera, embudos y posibles cierres."
  },
  fuerza_comercial_diego: {
    name: "Fuerza Comercial",
    area: "Comercial",
    description: "Seguimiento de negociaciones, responsables, posibles cierres y actividad de la fuerza comercial de Diego."
  },
  informe_gerencia_2026_2027: {
    name: "Informe Gerencia 2026 y 2027",
    area: "Gerencia",
    description: "Panel ejecutivo para seguimiento gerencial de indicadores 2026 y 2027."
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
      ["Estado de cartera", "Distribución entre cartera en tiempo, seguimiento y mora.", "donut"],
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
    title: "Posible cierre",
    description: "Monto y casos en las etapas comerciales de posible cierre.",
    blocks: [
      ["(COM) Posible Cierre RCH", "Monto y casos por etapa de posible cierre RCH.", "table"],
      ["(COM) Posible Cierre PNNC", "Monto y casos por etapa de posible cierre PNNC.", "table"]
    ]
  }
];

const generalManagementSection = {
  id: "gerencial",
  icon: "◇",
  title: "Indicadores gerenciales",
  description: "Metas, acumulados, posibles cierres y cumplimiento por línea comercial.",
  blocks: [
    ["Posible cierre general", "Monto proyectado por etapa para 1116, PNNC y RCH.", "management-close"],
    ["Detalle cumplimiento PNNC 2025", "Meta y cumplimiento mensual consolidado de PNNC y LP-2445.", "management-compliance"],
    ["Detalle cumplimiento RCH 2026", "Meta, valor alcanzado y porcentaje mensual de RCH Operativa.", "management-compliance"],
    ["Detalle cumplimiento 1116 2026", "Meta, valor alcanzado y porcentaje mensual de 1116 Operativa.", "management-compliance"]
  ]
};

const generalBlockCodes = {
  "Valores radicados por asesor": "radicated_values",
  "Total de negociaciones por asesor": "advisor_negotiations",
  "Valores radicados por coordinador": "coordinator_values",
  "Valores radicados por líder": "leader_values",
  "Detalle de coordinadores": "coordinator_detail",
  "Detalle de radicaciones por líder": "leader_detail",
  "Comisiones por asesor": "advisor_commissions",
  "Estado de cartera": "portfolio_state",
  "Cartera recaudada": "portfolio_collected",
  "Embudo Insolvencia": "funnel_insolvency",
  "Embudo RCH": "funnel_rch",
  "(COM) Posible Cierre RCH": "commercial_possible_close_rch",
  "(COM) Posible Cierre PNNC": "commercial_possible_close_pnnc"
};
let generalBlockAccess = { configured: false, codes: new Set() };
const isGeneralBlockVisible = (title) => {
  const code = generalBlockCodes[title];
  if (!generalBlockAccess.configured) return true;
  if (generalBlockAccess.codes.has(code)) return true;
  return code?.startsWith("commercial_possible_close_") && generalBlockAccess.codes.has("commercial_possible_close");
};
let teamScope = null;
let commercialDateRangeTouched = false;
let generalRadicatedData = null;
let generalDashboardData = null;
let commercialHierarchy = [];
let coordinatorRadicatedData = [];
const normalizeTeamValue = (value = "") => String(value ?? "")
  .normalize("NFD")
  .replace(/[\u0300-\u036f]/g, "")
  .replace(/[._-]+/g, " ")
  .replace(/\s+/g, " ")
  .trim()
  .toLocaleLowerCase("es-CO");
const isAdvisorTeamScope = () => normalizeTeamValue(teamScope?.roleLabel ?? "") === "advisor";
const isLeaderTeamScope = () => normalizeTeamValue(teamScope?.departmentName ?? "").includes("lider");
const scopedMemberNames = () => teamScope ? (teamScope.memberNames ?? []) : null;
const isTeamMember = (name) => {
  const members = scopedMemberNames();
  return !members || new Set(members.map(normalizeTeamValue)).has(normalizeTeamValue(name));
};
const isTeamDepartment = (name) => !teamScope || new Set((teamScope.departmentNames ?? []).map(normalizeTeamValue)).has(normalizeTeamValue(name));
const isScopedTeamMember = (name) => {
  if (!teamScope) return true;
  const hierarchyMembers = new Set(commercialHierarchy.map((item) => normalizeTeamValue(item.advisor ?? "")).filter(Boolean));
  return hierarchyMembers.size ? hierarchyMembers.has(normalizeTeamValue(name)) : isTeamMember(name);
};

const blockPreview = (type) => {
  if (type.startsWith("management-")) return `<div class="management-placeholder"><span></span><span></span><span></span></div>`;
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

const spanishMonthLabels = {
  "01": "01 ENE", "02": "02 FEB", "03": "03 MAR", "04": "04 ABR",
  "05": "05 MAY", "06": "06 JUN", "07": "07 JUL", "08": "08 AGO",
  "09": "09 SEP", "10": "10 OCT", "11": "11 NOV", "12": "12 DIC"
};

const percentFormatter = new Intl.NumberFormat("es-CO", {
  style: "percent",
  minimumFractionDigits: 1,
  maximumFractionDigits: 1
});

const gerenciaNumberFormatter = new Intl.NumberFormat("en-US", {
  maximumFractionDigits: 0
});

const gerenciaPercentFormatter = new Intl.NumberFormat("en-US", {
  style: "percent",
  minimumFractionDigits: 1,
  maximumFractionDigits: 1
});

const gerenciaDecimalFormatter = new Intl.NumberFormat("en-US", {
  minimumFractionDigits: 0,
  maximumFractionDigits: 2
});

const escapeHtml = (value) => String(value ?? "").replace(/[&<>"']/g, (character) => ({
  "&": "&amp;",
  "<": "&lt;",
  ">": "&gt;",
  "\"": "&quot;",
  "'": "&#039;"
}[character]));

const gerenciaMonthOrder = ["01 ENE", "02 FEB", "03 MAR", "04 ABR", "05 MAY", "06 JUN", "07 JUL", "08 AGO", "09 SEP", "10 OCT", "11 NOV", "12 DIC"];
const gerenciaMonthNames = {
  "01 ENE": "Enero",
  "02 FEB": "Febrero",
  "03 MAR": "Marzo",
  "04 ABR": "Abril",
  "05 MAY": "Mayo",
  "06 JUN": "Junio",
  "07 JUL": "Julio",
  "08 AGO": "Agosto",
  "09 SEP": "Septiembre",
  "10 OCT": "Octubre",
  "11 NOV": "Noviembre",
  "12 DIC": "Diciembre"
};
const formatGerenciaMonthFilterLabel = (month) => gerenciaMonthNames[month] ?? month;
const getGerenciaSelectedMonths = () => [...document.querySelectorAll("[data-gerencia-month-filter]:checked")]
  .map((input) => input.value);
const hasGerenciaMonthFilter = () => getGerenciaSelectedMonths().length > 0;
const getGerenciaSelectedMonth = () => getGerenciaSelectedMonths()[0] ?? "";
const getGerenciaSelectedMonthLabel = () => {
  const months = getGerenciaSelectedMonths();
  return months.length ? months.map(formatGerenciaMonthFilterLabel).join(", ") : "Todos";
};
const getMonthLabelFromNumber = (value) => {
  const number = Number(value);
  return Number.isFinite(number) && number >= 1 && number <= 12 ? gerenciaMonthOrder[number - 1] : "";
};
const getRowMonthLabel = (row) => row?.month ?? row?.mes ?? row?.Meses ?? row?.Mes ?? getMonthLabelFromNumber(row?.monthNumber);
const filterRowsByGerenciaMonth = (rows) => {
  const selectedMonths = getGerenciaSelectedMonths();
  if (!selectedMonths.length) return rows;
  const selected = new Set(selectedMonths);
  return rows.filter((row) => selected.has(getRowMonthLabel(row)));
};
const getFilteredTotals = (rows, fields) => fields.reduce((totals, field) => {
  totals[field] = rows.reduce((sum, row) => sum + Number(row[field] ?? 0), 0);
  return totals;
}, {});
const getFilteredSummary = (rows, fields) => getFilteredTotals(filterRowsByGerenciaMonth(rows), fields);
const syncGerenciaMonthAllState = () => {
  const all = document.getElementById("gerenciaMonthAll");
  const summary = document.getElementById("gerenciaMonthSummary");
  const months = getGerenciaSelectedMonths();
  if (all) all.checked = !months.length;
  if (!summary) return;
  summary.textContent = !months.length
    ? "Todos"
    : months.length <= 3
      ? months.map(formatGerenciaMonthFilterLabel).join(", ")
      : `${months.length} meses seleccionados`;
};
let gerenciaMonthlyRows = [];
let gerenciaMonthlySummary = { year: 2026, totalAchieved: 0, totalCompliance: 0 };
let gerenciaSort = { key: "compliance", direction: "desc" };
let gerenciaChartSeries = { meta: true, radicado: true };
let pnncDetailRows = [];
let pnncDetailSummary = { year: 2026, totalAchieved: 0, complianceSummary: 0 };
let pnncChartSeries = { meta: true, radicado: true };
let pnncDetailSort = { key: "compliance", direction: "desc" };
let operativaRchRows = [];
let operativaRchBankRows = [];
let operativaRchApprovedRows = [];
let operativaRchApprovedSummary = { year: 2026, totalCases: 0, totalAmount: 0 };
let operativaRchApprovedChartSeries = { amount: true };
let operativaRchSummary = { year: 2026, totalStarted: 0, totalFinished: 0 };
let operativaRchSort = { key: "started", direction: "desc" };
let operativaRchChartSeries = { started: true, finished: true };
let operativaRchBankPage = 0;
let operativaRchActiveBanks = new Set();
let pnnc2025ProcessRows = [];
let pnnc2025ProcessSummary = { year: 2025, totalStarted: 0, totalFinished: 0 };
let pnnc2025ProcessSort = { key: "month", direction: "asc" };
let pnnc2025ProcessChartSeries = { started: true, finished: true };
let operativaPnncRows = [];
let operativaPnncSummary = { totalClients: 0, totalOutOfManagement: 0, totalParticipation: null };
let operativaPnncSort = { key: "clients", direction: "desc" };
let operativaPnncSecondRows = [];
let operativaPnncSecondSort = { key: "stageOrder", direction: null };
let operativaPnncDetailRows = [];
let operativaPnncDetailSort = { key: "name", direction: "asc" };
let lpMonthlyTaskRows = [];
let lpMonthlyTaskSummary = {};
let lpMonthlyTaskSort = { key: "month", direction: "asc" };
let lpWeeklyTaskRows = [];
let lpWeeklyTaskSummary = {};
let lpWeeklyTaskSort = { key: "weekNumber", direction: "asc" };
let lpEmbargosTaskRows = [];
let lpEmbargosTaskSummary = {};
let lpEmbargosTaskSort = { key: "month", direction: "asc" };
let lpLibranzaTaskRows = [];
let lpLibranzaTaskSummary = {};
let lpLibranzaTaskSort = { key: "month", direction: "asc" };
let insEmbargosRows = [];
let insEmbargosSort = { key: "name", direction: "asc" };
let insLibranzaRows = [];
let insLibranzaSort = { key: "name", direction: "asc" };
let insuranceKpiRows = [];
let insuranceCommercialRows = [];
let insuranceKpiSort = { key: "monthNumber", direction: "asc" };
let insuranceCommercialSort = { key: "monthNumber", direction: "asc" };
let insuranceCompliance = 0;
let insuranceCallsRows = [];
let insuranceQuotesRows = [];
let insuranceCallsSort = { key: "monthNumber", direction: "asc" };
let insuranceQuotesSort = { key: "monthNumber", direction: "asc" };
let insuranceOutRows = [];
let insuranceOutDetailRows = [];
let insuranceOutTotals = {};
let insuranceOutSort = { key: "stage", direction: "asc" };
let insuranceOutDetailSort = { key: "stage", direction: "asc" };
let insuranceOutDetailSearch = "";
let customerServiceRequirements = [];
let customerServiceMonthlyRows = [];
let customerServiceActiveRequirements = new Set();
let customerServiceMonthlySeries = { received: true };
let customerServiceResponseRows = [];
let customerServiceActiveResponseRequirements = new Set();
let customerServiceWithdrawals = {
  insolvencySummary: [],
  rchSummary: [],
  insolvencyDetail: [],
  rchDetail: []
};
let customerServiceSummary = { compliance: 0, received: 0 };
let diegoHierarchyLoaded = false;
let diegoCommercialDataLoaded = false;
let diegoAutoRefreshTimer = 0;
const diegoAutoRefreshMs = 30 * 60 * 1000;

const markCommercialViewPending = () => {
  if (["fuerza_comercial_diego", "informe_general_comercial"].includes(reportId)) {
    setText("reportStatus", diegoCommercialDataLoaded ? "Pendiente" : "Sin cargar");
  }
};

const ensureDiegoFilterHierarchy = async () => {
  if (diegoHierarchyLoaded) return;
  try {
    await loadDiegoFilterHierarchy();
  } catch (error) {
    console.warn("La jerarquÃ­a comercial aÃºn no estÃ¡ disponible; el informe continuarÃ¡ cargando sus tablas.", error);
    commercialHierarchy = [];
  }
  diegoHierarchyLoaded = true;
};

const startDiegoAutoRefresh = () => {
  if (!["fuerza_comercial_diego", "informe_general_comercial"].includes(reportId) || diegoAutoRefreshTimer) return;
  diegoAutoRefreshTimer = window.setInterval(() => {
    if (document.visibilityState === "visible") window.location.reload();
  }, diegoAutoRefreshMs);
};


const loadDiegoRadicatedValues = async () => {
  const container = document.getElementById("diegoValoresRadicados");
  const queryString = getDiegoCommercialQueryString();

  try {
    const response = await fetch(`/api/reports/fuerza-comercial-diego/valores-radicados?${queryString}`, { cache: "no-store" });
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    const data = await response.json();
    if (teamScope) {
      data.items = (data.items ?? []).filter((item) =>
        isTeamDepartment(item.coordinator)
        || isTeamDepartment(item.leader)
        || isScopedTeamMember(item.advisor));
    }
    generalRadicatedData = data;

    if (!data.items?.length) {
      container.innerHTML = `<div class="empty-block"><strong>Sin valores radicados para ${data.year}</strong><span>Sincronice los negocios de Bitrix para cargar este indicador.</span></div>`;
      return;
    }

    const months = [...new Set(data.items.map((item) => item.month))]
      .sort((left, right) => Number.parseInt(left, 10) - Number.parseInt(right, 10));
    const advisorValues = new Map();
    const advisorMeta = new Map();
    data.items.forEach((item) => {
      if (!advisorValues.has(item.advisor)) advisorValues.set(item.advisor, new Map());
      const monthlyValues = advisorValues.get(item.advisor);
      monthlyValues.set(item.month, (monthlyValues.get(item.month) ?? 0) + item.totalAchieved);
      if (!advisorMeta.has(item.advisor)) advisorMeta.set(item.advisor, { coordinators: new Set(), leaders: new Set(), lines: new Set() });
      const meta = advisorMeta.get(item.advisor);
      if (item.coordinator) meta.coordinators.add(item.coordinator);
      if (item.leader) meta.leaders.add(item.leader);
      if (item.pipeline) meta.lines.add(normalizeFilterText(item.pipeline).includes("pnnc") ? "pnnc" : normalizeFilterText(item.pipeline));
    });
    const advisors = [...advisorValues.keys()].sort((left, right) => left.localeCompare(right, "es"));
    const dataSet = (values) => encodeURIComponent([...values].join("\u0001"));

    container.innerHTML = `
      <div class="radicated-table-wrap radicated-matrix-wrap">
        <table class="radicated-table radicated-matrix">
          <thead>
            <tr><th rowspan="2">Asesor</th><th colspan="${months.length}">Total alcanzado</th></tr>
            <tr>${months.map((month) => `<th data-month="${month.slice(0, 2)}">${month}</th>`).join("")}</tr>
          </thead>
          <tbody>${advisors.map((advisor) => `
          <tr data-advisor="${encodeURIComponent(advisor)}" data-coordinator="${dataSet(advisorMeta.get(advisor)?.coordinators ?? [])}" data-leader="${dataSet(advisorMeta.get(advisor)?.leaders ?? [])}" data-line="${dataSet(advisorMeta.get(advisor)?.lines ?? [])}"><td>${advisor}</td>${months.map((month) => {
              const value = advisorValues.get(advisor).get(month);
              return `<td data-month="${month.slice(0, 2)}">${value ? formatNumber.format(value) : ""}</td>`;
            }).join("")}</tr>
          `).join("")}</tbody>
        </table>
      </div>`;
    decorateTableTotals(container);
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
  "Estado de cartera": "(COM) Estado de cartera",
  "Cartera recaudada": "(COM) Cartera Recaudada",
  "Embudo Insolvencia": "(COM) Embudo Insolvencia",
  "Embudo RCH": "(COM) Embudo RCH",
  "(COM) Posible Cierre RCH": "(COM) Posible Cierre RCH",
  "(COM) Posible Cierre PNNC": "(COM) Posible Cierre PNNC"
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

const parseTableNumber = (text = "") => {
  const raw = text.trim();
  if (!raw || /^(n\/?a|—|-)$/i.test(raw)) return null;
  const isPercent = raw.includes("%");
  let normalized = raw.replace(/[^\d,.-]/g, "");
  if (!normalized || normalized === "-") return null;
  if (normalized.includes(",") && normalized.includes(".")) {
    normalized = normalized.replace(/\./g, "").replace(",", ".");
  } else if (/^-?\d+[.,]\d{1,2}$/.test(normalized)) {
    normalized = normalized.replace(",", ".");
  } else {
    normalized = normalized.replace(/[.,]/g, "");
  }
  const value = Number(normalized);
  return Number.isFinite(value) ? { value, isPercent } : null;
};

const tableLeafHeaders = (table, columnCount) => {
  const rows = [...table.tHead?.rows ?? []];
  const labels = Array(columnCount).fill("");
  rows.forEach((row) => [...row.cells].forEach((cell) => {
    const start = cell.cellIndex;
    const span = Math.max(1, cell.colSpan || 1);
    if (span === 1 || row === rows.at(-1)) labels[start] = cell.textContent.trim();
  }));
  if (rows.length > 1 && rows.at(-1).cells.length === columnCount - 1) {
    labels[0] = rows[0].cells[0]?.textContent.trim() || labels[0];
    [...rows.at(-1).cells].forEach((cell, index) => { labels[index + 1] = cell.textContent.trim(); });
  }
  return labels.map((label, index) => label || `Columna ${index + 1}`);
};

const enableTableSorting = (table, columnCount) => {
  if (!table.tHead || table.dataset.sortableReady === "true") return;
  const headerRows = [...table.tHead.rows];
  const lastHeaderRow = headerRows.at(-1);

  headerRows.forEach((row) => {
    let logicalIndex = row === lastHeaderRow && row.cells.length < columnCount
      ? Math.max(0, columnCount - row.cells.length - (headerRows[0].cells[headerRows[0].cells.length - 1]?.rowSpan > 1 ? 1 : 0))
      : 0;
    [...row.cells].forEach((header) => {
      const columnIndex = logicalIndex;
      logicalIndex += Math.max(1, header.colSpan || 1);
      if (header.colSpan > 1 || header.querySelector("button") || columnIndex >= columnCount) return;
      const label = header.textContent.trim();
      if (!label) return;
      const button = document.createElement("button");
      button.type = "button";
      button.className = "table-column-sort";
      button.dataset.column = String(columnIndex);
      button.dataset.direction = "none";
      button.setAttribute("aria-label", `Ordenar ${label} de mayor a menor`);
      button.innerHTML = `<span>${label}</span><i aria-hidden="true"></i>`;
      header.textContent = "";
      header.appendChild(button);
    });
  });

  table.tHead.addEventListener("click", (event) => {
    const button = event.target.closest(".table-column-sort");
    if (!button) return;
    const columnIndex = Number(button.dataset.column);
    const direction = button.dataset.direction === "desc" ? "asc" : "desc";
    table.querySelectorAll(".table-column-sort").forEach((candidate) => {
      candidate.dataset.direction = "none";
      candidate.removeAttribute("aria-sort");
    });
    button.dataset.direction = direction;
    button.setAttribute("aria-sort", direction === "desc" ? "descending" : "ascending");
    button.setAttribute("aria-label", `Ordenar ${button.querySelector("span").textContent} de ${direction === "desc" ? "menor a mayor" : "mayor a menor"}`);

    [...table.tBodies].forEach((body) => {
      const rows = [...body.rows];
      rows.sort((leftRow, rightRow) => {
        const leftText = leftRow.cells[columnIndex]?.textContent.trim() ?? "";
        const rightText = rightRow.cells[columnIndex]?.textContent.trim() ?? "";
        const leftNumber = parseTableNumber(leftText)?.value;
        const rightNumber = parseTableNumber(rightText)?.value;
        let comparison;
        if (leftNumber != null && rightNumber != null) comparison = leftNumber - rightNumber;
        else if (leftNumber != null) comparison = -1;
        else if (rightNumber != null) comparison = 1;
        else comparison = leftText.localeCompare(rightText, "es", { numeric: true, sensitivity: "base" });
        return direction === "desc" ? -comparison : comparison;
      });
      rows.forEach((row) => body.appendChild(row));
    });
  });
  table.dataset.sortableReady = "true";
};

const decorateTableTotals = (root = document) => {
  root.querySelectorAll("table").forEach((table) => {
    const bodyRows = [...table.tBodies].flatMap((body) => [...body.rows]).filter((row) => !row.hidden);
    if (!bodyRows.length) return;
    const columnCount = Math.max(...bodyRows.map((row) => row.cells.length));
    if (columnCount < 2) return;
    enableTableSorting(table, columnCount);
    const headers = tableLeafHeaders(table, columnCount);
    const totals = Array(columnCount).fill(0);
    const numericCounts = Array(columnCount).fill(0);
    const percentColumns = Array(columnCount).fill(false);

    bodyRows.forEach((row) => [...row.cells].forEach((cell, index) => {
      if (cell.hidden) return;
      const parsed = parseTableNumber(cell.textContent);
      if (!parsed) return;
      totals[index] += parsed.value;
      numericCounts[index] += 1;
      percentColumns[index] ||= parsed.isPercent || /%|tasa|cumplimiento/i.test(headers[index]);
    }));

    const numericIndexes = numericCounts.map((count, index) => count ? index : -1).filter((index) => index > 0);
    if (!numericIndexes.length) return;

    let footer = table.querySelector("tfoot[data-auto-totals]");
    if (!table.tFoot) {
      footer = document.createElement("tfoot");
      footer.dataset.autoTotals = "true";
      table.appendChild(footer);
    }
    if (footer) {
      if (table.classList.contains("advisor-negotiations-table")) {
        const negotiations = totals[1] ?? 0;
        const studies = totals[2] ?? 0;
        const radicated = totals[4] ?? 0;
        const studiesRate = negotiations ? (studies / negotiations) * 100 : 0;
        const closingRate = negotiations ? (radicated / negotiations) * 100 : 0;
        footer.innerHTML = `<tr>
          <th>Total</th>
          <td>${formatNumber.format(negotiations)}</td>
          <td>${formatNumber.format(studies)}</td>
          <td>${studiesRate.toLocaleString("es-CO", { minimumFractionDigits: 1, maximumFractionDigits: 1 })}%</td>
          <td>${formatNumber.format(radicated)}</td>
          <td>${closingRate.toLocaleString("es-CO", { minimumFractionDigits: 1, maximumFractionDigits: 1 })}%</td>
        </tr>`;
        return;
      }
      footer.innerHTML = `<tr>${Array.from({ length: columnCount }, (_, index) => {
        if (index === 0) return "<th>Total</th>";
        if (!numericCounts[index]) return "<td>—</td>";
        if (percentColumns[index]) {
          const average = totals[index] / numericCounts[index];
          return `<td>${average.toLocaleString("es-CO", { minimumFractionDigits: 1, maximumFractionDigits: 1 })}%</td>`;
        }
        return `<td>${formatNumber.format(totals[index])}</td>`;
      }).join("")}</tr>`;
    }

    const wrap = table.closest(".radicated-table-wrap, .synced-table-wrap") || table.parentElement;
    if (wrap?.previousElementSibling?.classList.contains("table-total-summary")) {
      wrap.previousElementSibling.remove();
    }
  });
};

const renderPipelineFunnel = (items) => {
  const visibleItems = [...items]
    .filter((item) => Number(item.cases ?? 0) > 0)
    .sort((left, right) =>
      Number(left.sortOrder ?? 9999) - Number(right.sortOrder ?? 9999)
      || String(left.stage ?? "").localeCompare(String(right.stage ?? ""), "es", { sensitivity: "base" }));
  if (!visibleItems.length) {
    return `<div class="empty-block"><strong>Sin datos</strong><span>No hay etapas con casos para este embudo.</span></div>`;
  }
  const steps = Math.max(visibleItems.length - 1, 1);
  return `
    <div class="pipeline-funnel" role="list">
      ${visibleItems.map((item, index) => {
        const cases = Number(item.cases ?? 0);
        const stage = String(item.stage ?? "Sin etapa");
        const stageParts = stage.match(/^(\d{1,2})\s+(.+)$/);
        const stageNumber = stageParts?.[1] ?? String(index + 1).padStart(2, "0");
        const stageName = stageParts?.[2] ?? stage;
        const width = visibleItems.length === 1 ? 94 : 94 - ((index / steps) * 34);
        return `
          <article class="pipeline-funnel-step" role="listitem" style="--funnel-width:${width.toFixed(1)}%">
            <div class="pipeline-funnel-bar" title="${escapeHtml(stage)}: ${formatNumber.format(cases)}">
              <span class="pipeline-funnel-stage" title="${escapeHtml(stage)}"><b>${escapeHtml(stageNumber)}</b><span>${escapeHtml(stageName)}</span></span>
              <strong>${formatNumber.format(cases)}</strong>
            </div>
          </article>`;
      }).join("")}
    </div>`;
};

const renderPipelineTable = (items, mode) => {
  if (mode === "funnel") return renderPipelineFunnel(items);
  const sortedItems = [...items].sort((left, right) => right.cases - left.cases);
  const headers = mode === "commercial"
      ? ["ETAPA COMERCIAL RCH", "# CASOS COMERCIAL", "$ VALOR COMERCIAL"]
      : mode === "operative"
        ? ["ETAPA OPERATIVA RCH", "# CASOS OPERATIVA", "$ VALOR COMERCIAL"]
        : mode === "pnnc-commercial"
          ? ["ETAPA COMERCIAL PNNC", "# CASOS COMERCIAL PNNC", "$ VALOR COMERCIAL"]
          : ["ETAPA OPERATIVA PNNC", "# CASOS OPERATIVA PNNC", "$ VALOR OPERATIVA"];
  const rows = sortedItems.map((item) => {
    return `<tr><td>${item.stage}</td><td>${formatNumber.format(item.cases)}</td><td>${formatNumber.format(item.totalValue)}</td></tr>`;
  });
  return renderDataTable(headers, rows, `pipeline-table pipeline-table-${mode}`);
};

const renderMonthlyMatrix = (groupLabel, items, groupField, roundValues = false) => {
  const valueFormatter = roundValues
    ? new Intl.NumberFormat("es-CO", { maximumFractionDigits: 0 })
    : formatNumber;
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
          <tr data-${groupField}="${encodeURIComponent(group)}"><td>${group}</td>${months.map((month) => {
            const value = groupedValues.get(group).get(month);
            return `<td data-month="${month.slice(0, 2)}">${value ? valueFormatter.format(value) : ""}</td>`;
          }).join("")}</tr>
        `).join("")}</tbody>
      </table>
    </div>`;
};

const monthlyLeaderGoal = (month) => Number.parseInt(month, 10) >= 7 ? 70000000 : 60000000;
const isKnownHierarchyGroupName = (value = "", fallbackLabel = "") => {
  const normalized = normalizeFilterText(value);
  return Boolean(normalized) && normalized !== normalizeFilterText(fallbackLabel);
};
const isCoordinatorGroupName = (value = "") => isKnownHierarchyGroupName(value, "Sin coordinador");
const isLeaderGroupName = (value = "") => isKnownHierarchyGroupName(value, "Sin líder");

const renderPerformanceTable = (items, groupField, coordinatorMode = false) => {
  const groupLabel = groupField === "coordinator" ? "Coordinador" : "Líder";
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
        <thead><tr><th>${groupLabel}</th><th>Mes</th><th>Meta</th><th>Valor alcanzado</th><th>% de cumplimiento</th></tr></thead>
        <tbody>${rows.map((row) => `<tr data-group="${encodeURIComponent(row.group)}" data-${groupField}="${encodeURIComponent(row.group)}"><td>${row.group}</td><td>${row.month}</td><td>${formatNumber.format(row.goal)}</td><td>${formatNumber.format(row.total)}</td><td>${row.compliance.toLocaleString("es-CO", { minimumFractionDigits: 1, maximumFractionDigits: 1 })}%</td></tr>`).join("")}</tbody>
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
  const advisorTotal = (advisor) => [...advisorValues.get(advisor).values()].reduce((sum, value) => sum + value, 0);
  const advisors = [...advisorValues.keys()].sort((left, right) =>
    advisorTotal(right) - advisorTotal(left)
    || left.localeCompare(right, "es", { sensitivity: "base" }));
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
        return `<tr data-advisor="${encodeURIComponent(advisor)}"><td>${advisor}</td>${months.map((month) => `<td data-month="${month.slice(0, 2)}">${values.get(month) ? formatNumber.format(values.get(month)) : ""}</td>`).join("")}<td>${formatNumber.format(advisorTotal)}</td></tr>`;
      }).join("")}</tbody>
      <tfoot><tr><th>Total (Sum)</th>${months.map((month) => `<td data-month="${month.slice(0, 2)}">${formatNumber.format(monthTotals.get(month))}</td>`).join("")}<td>${formatNumber.format(grandTotal)}</td></tr></tfoot>
    </table>
  </div>`;
};

const replaceBlockPreview = (title, content, count) => {
  const block = findDiegoBlock(title);
  if (!block) return;
  block.querySelector(".diego-block-title em").textContent = `${count} registros`;
  const preview = block.querySelector(".block-table, .block-bars, .block-funnel, .block-donut, .pipeline-funnel, .management-placeholder, .management-kpi-grid, .radicated-table-wrap, .empty-block");
  if (preview) preview.outerHTML = content;
  decorateTableTotals(block);
};

const commercialGoals = {
  annual: 46190000000,
  monthly: 46190000000,
  weekly: 10545450000,
  pipelines: { PNNC: 1250000000, RCH: 1250000000, "1116": 750000000 }
};

const normalizeMonthLabel = (value = "") => {
  const normalized = normalizeFilterText(value);
  return Object.values(spanishMonthLabels).find((label) => normalized.includes(normalizeFilterText(label.slice(3))) || normalized === normalizeFilterText(label)) ?? value;
};

const renderManagementKpis = (data) => {
  const items = data.items ?? [];
  const annual = items.reduce((sum, item) => sum + Number(item.totalAchieved ?? 0), 0);
  const annualGoal = Number(data.annualGoal ?? 0) || commercialGoals.annual;
  const goalsByMonth = new Map();
  (data.monthlyGoals ?? []).forEach((item) => {
    const month = normalizeMonthLabel(item.month);
    goalsByMonth.set(month, (goalsByMonth.get(month) ?? 0) + Number(item.goal ?? 0));
  });
  const valuesByMonth = new Map();
  items.forEach((item) => {
    const month = normalizeMonthLabel(item.month);
    valuesByMonth.set(month, (valuesByMonth.get(month) ?? 0) + Number(item.totalAchieved ?? 0));
  });
  const annualRate = annualGoal ? annual / annualGoal : 0;
  const fallbackMonthlyGoal = annualGoal ? annualGoal / 12 : 0;
  const monthlyRate = [...valuesByMonth.entries()].reduce((sum, [month, value]) => {
    const goal = goalsByMonth.get(month) ?? fallbackMonthlyGoal;
    return sum + (goal ? value / goal : 0);
  }, 0);
  const cards = [
    ["(GER) Porcentaje Acumulado Anual Comercial 2026", `${(annualRate * 100).toLocaleString("es-CO", { maximumFractionDigits: 1 })}%`],
    ["(GER) Porcentaje Acumulado Mensual Comercial 2026", monthlyRate.toLocaleString("es-CO", { maximumFractionDigits: 2 })],
    ["(GER) $ Acumulado Anual Comercial 2026", formatNumber.format(annual)],
    ["(GER) Total Radicado Comercial General Mensual DEF 2026", formatNumber.format(annual)],
    ["(COM) Meta anual 2026", formatNumber.format(annualGoal)],
    ["(COM) Meta Mensual 2026", formatNumber.format(annualGoal)],
    ["(COM) Meta Semanal 2026", formatNumber.format(commercialGoals.weekly)]
  ];
  return `<div class="management-kpi-grid">${cards.map(([label, value]) => `<article><span>${label}</span><strong>${value}</strong></article>`).join("")}</div>`;
};

const renderGeneralPossibleClose = (items) => {
  const pipelines = ["1116", "PNNC", "RCH"];
  const stages = [...new Set(items.map((item) => item.stage))].sort();
  const valueFor = (stage, pipeline) => items.filter((item) => item.stage === stage && item.pipeline === pipeline).reduce((sum, item) => sum + Number(item.amount ?? 0), 0);
  const totals = new Map(pipelines.map((pipeline) => [pipeline, items.filter((item) => item.pipeline === pipeline).reduce((sum, item) => sum + Number(item.amount ?? 0), 0)]));
  const grandTotal = [...totals.values()].reduce((sum, value) => sum + value, 0);
  return `<div class="radicated-table-wrap"><table class="radicated-table synced-table management-close-table"><thead><tr><th>Etapa</th>${pipelines.map((pipeline) => `<th>${pipeline}</th>`).join("")}<th>Total (Sum)</th></tr></thead><tbody>${stages.map((stage) => { const rowTotal = pipelines.reduce((sum, pipeline) => sum + valueFor(stage, pipeline), 0); return `<tr><td>${stage}</td>${pipelines.map((pipeline) => `<td>${formatNumber.format(valueFor(stage, pipeline))}</td>`).join("")}<td>${formatNumber.format(rowTotal)}</td></tr>`; }).join("")}</tbody><tfoot><tr><th>Total (Sum)</th>${pipelines.map((pipeline) => `<td>${formatNumber.format(totals.get(pipeline))}</td>`).join("")}<td>${formatNumber.format(grandTotal)}</td></tr></tfoot></table></div>`;
};

const commercialPossibleCloseLine = (item) => {
  const pipeline = normalizeFilterText(item.pipeline || "");
  if (pipeline.includes("rch")) return "rch";
  if (pipeline.includes("pnnc") || pipeline.includes("insolvencia")) return "pnnc";
  const commercialLine = normalizeFilterText(item.commercialLine || "");
  if (commercialLine.includes("rch")) return "rch";
  if (commercialLine.includes("pnnc") || commercialLine.includes("insolvencia")) return "pnnc";
  return commercialLine || pipeline;
};

const commercialPossibleCloseStages = [
  "01 Revisión líder",
  "02 Radicación por validar",
  "03 Documentación pendiente",
  "04 Documentación subsanada"
];

const buildCommercialPossibleCloseGroups = (items) => {
  const grouped = new Map();
  items.forEach((item) => {
    const pipeline = item.pipeline ?? "Sin pipeline";
    const stage = item.stage ?? "Sin etapa";
    const key = `${pipeline}\u0001${stage}`;
    if (!grouped.has(key)) grouped.set(key, { pipeline, stage, amount: 0, cases: 0, line: commercialPossibleCloseLine(item) });
    const row = grouped.get(key);
    row.amount += Number(item.amount ?? 0);
    row.cases += Number(item.cases ?? 0);
  });

  const pipelines = ["RCH", "PNNC"].map((pipeline) => {
    const line = commercialPossibleCloseLine({ pipeline });
    const rows = commercialPossibleCloseStages
      .map((stage) => grouped.get(`${pipeline}\u0001${stage}`) ?? { pipeline, stage, amount: 0, cases: 0, line });
    const amount = rows.reduce((sum, row) => sum + row.amount, 0);
    const cases = rows.reduce((sum, row) => sum + row.cases, 0);
    return { pipeline, line, rows, amount, cases };
  }).filter((group) => group.rows.some((row) => row.amount || row.cases));
  let grandAmount = 0;
  let grandCases = 0;
  let count = 0;
  pipelines.forEach((group) => {
    const pipelineAmount = group.rows.reduce((sum, row) => sum + row.amount, 0);
    const pipelineCases = group.rows.reduce((sum, row) => sum + row.cases, 0);
    grandAmount += pipelineAmount;
    grandCases += pipelineCases;
    count += group.rows.length;
  });
  return { pipelines, grandAmount, grandCases, count };
};

const renderCommercialPossibleCloseTable = (group, totals = null) => `
  <div class="commercial-possible-close-section">
    <table class="radicated-table synced-table commercial-possible-close-table">
      <thead><tr><th>Metric</th><th>MONTO</th><th>SUM(CASOS)</th></tr></thead>
      <tbody>
        <tr class="commercial-possible-close-group" data-summary-row="true" data-line="${group.line}"><th>PIPELINE</th><td>${escapeHtml(group.pipeline)}</td><td>${escapeHtml(group.pipeline)}</td></tr>
        <tr class="commercial-possible-close-subgroup" data-summary-row="true" data-line="${group.line}"><th>ETAPA</th><td></td><td></td></tr>
        ${group.rows.map((row) => `<tr data-line="${group.line}" data-stage-row="true"><td>${escapeHtml(row.stage)}</td><td>${formatNumber.format(row.amount)}</td><td>${formatNumber.format(row.cases)}</td></tr>`).join("")}
      </tbody>
      ${totals ? `<tfoot><tr><th>Total (Sum)</th><td>${formatNumber.format(totals.amount)}</td><td>${formatNumber.format(totals.cases)}</td></tr></tfoot>` : ""}
    </table>
  </div>`;

const renderCommercialPossibleClose = (items, lineFilter = null) => {
  const filteredItems = lineFilter
    ? items.filter((item) => commercialPossibleCloseLine(item) === lineFilter)
    : items;
  const grouped = buildCommercialPossibleCloseGroups(filteredItems);
  if (!grouped.count) {
    return {
      count: 0,
      html: `<div class="empty-block"><strong>Sin posibles cierres</strong><span>No hay negocios en las etapas configuradas.</span></div>`
    };
  }
  const tables = grouped.pipelines.map((group) => (
    renderCommercialPossibleCloseTable(group, { amount: group.amount, cases: group.cases })
  ));
  return {
    count: grouped.count,
    html: `<div class="radicated-table-wrap synced-table-wrap commercial-possible-close-wrap">
      ${tables.join("")}
    </div>`
  };
};
const renderPipelineCompliance = (items, pipeline, monthlyGoals) => {
  const monthly = new Map();
  const includedPipelines = pipeline === "PNNC" ? new Set(["PNNC", "LP-2445"]) : new Set([pipeline]);
  items.filter((item) => includedPipelines.has(item.pipeline)).forEach((item) => monthly.set(item.month, (monthly.get(item.month) ?? 0) + Number(item.totalAchieved ?? 0)));
  const goals = new Map((monthlyGoals ?? []).filter((item) => item.pipeline === pipeline).map((item) => [normalizeMonthLabel(item.month), Number(item.goal ?? 0)]));
  const rows = [...monthly.entries()].sort(([left], [right]) => right.localeCompare(left)).map(([month, achieved]) => {
    const goal = goals.get(month) ?? commercialGoals.pipelines[pipeline];
    const rate = goal ? (achieved / goal) * 100 : 0;
    return `<tr><td>${month}</td><td>${formatNumber.format(goal)}</td><td>${formatNumber.format(achieved)}</td><td>${rate.toLocaleString("es-CO", { maximumFractionDigits: 1 })}%</td></tr>`;
  });
  const headers = pipeline === "PNNC"
    ? ["Meses", "Meta PNNC", "Cumplimiento PNNC", "% de Cumplimiento"]
    : ["Meses", `Meta ${pipeline}`, `Cumplimiento ${pipeline}`, "% de Cumplimiento"];
  return rows.length ? renderDataTable(headers, rows, "management-compliance-table") : `<div class="empty-block"><strong>Sin resultados para ${pipeline}</strong><span>No hay valores radicados de esta línea para el periodo.</span></div>`;
};

const renderGeneralManagement = () => {
  if (reportId !== "informe_general_comercial" || !generalRadicatedData || !generalDashboardData) return;
  const items = generalRadicatedData.items ?? [];
  const closeItems = generalDashboardData.possibleCloseGeneral ?? [];
  replaceBlockPreview("Posible cierre general", closeItems.length ? renderGeneralPossibleClose(closeItems) : `<div class="empty-block"><strong>Sin posibles cierres</strong><span>No hay negocios en las etapas configuradas.</span></div>`, closeItems.length);
  [["Detalle cumplimiento PNNC 2025", "PNNC"], ["Detalle cumplimiento RCH 2026", "RCH"], ["Detalle cumplimiento 1116 2026", "1116"]].forEach(([title, pipeline]) => {
    const count = items.filter((item) => pipeline === "PNNC" ? ["PNNC", "LP-2445"].includes(item.pipeline) : item.pipeline === pipeline).length;
    replaceBlockPreview(title, renderPipelineCompliance(items, pipeline, generalRadicatedData.monthlyGoals), count);
  });
};

const loadDiegoDashboardData = async () => {
  const queryString = getDiegoCommercialQueryString();
  const response = await fetch(`/api/reports/fuerza-comercial-diego/dashboard?${queryString}`, { cache: "no-store" });
  if (!response.ok) throw new Error(`HTTP ${response.status}`);
  const data = await response.json();
  if (teamScope) {
    data.advisors = (data.advisors ?? []).filter((item) => isScopedTeamMember(item.advisor));
    data.departments = (data.departments ?? []).filter((item) => isTeamDepartment(item.department));
    data.possibleCloseCommercial = (data.possibleCloseCommercial ?? []).filter((item) => isScopedTeamMember(item.advisor));
  }
  generalDashboardData = data;

  replaceBlockPreview("Total de negociaciones por asesor", renderDataTable(
    ["Asesor", "Total de negociaciones", "Estudios", "Estudios sobre total", "Radicados", "Tasa de cierre"],
    [...data.advisors]
      .sort((left, right) => left.advisor.localeCompare(right.advisor, "es", { sensitivity: "base" }))
      .map((item) => {
        const studiesRateValue = item.negotiations ? item.commercialCases / item.negotiations : 0;
        const closingRateValue = item.negotiations ? item.radicatedCases / item.negotiations : null;
        const studiesRate = `${(studiesRateValue * 100).toFixed(1)}%`;
        const closingRate = closingRateValue === null ? "N/A" : `${(closingRateValue * 100).toFixed(1)}%`;
        return `<tr data-advisor="${encodeURIComponent(item.advisor)}"><td>${item.advisor}</td><td>${formatNumber.format(item.negotiations)}</td><td>${formatNumber.format(item.commercialCases)}</td><td>${studiesRate}</td><td>${formatNumber.format(item.radicatedCases)}</td><td>${closingRate}</td></tr>`;
      }),
    "advisor-negotiations-table"
  ), data.advisors.length);


  const pipelineBlocks = {
    rch_comercial_funnel: ["Embudo RCH"],
    pnnc_comercial_funnel: ["Embudo Insolvencia"]
  };

  Object.entries(pipelineBlocks).forEach(([pipeline, titles]) => {
    const items = data.stages.filter((item) => item.pipeline === pipeline);
    titles.forEach((title) => {
      replaceBlockPreview(title, renderPipelineTable(items, "funnel"), items.filter((item) => Number(item.cases ?? 0) > 0).length);
    });
  });
  const possibleCloseItems = data.possibleCloseCommercial ?? [];
  const possibleCloseRchView = renderCommercialPossibleClose(possibleCloseItems, "rch");
  const possibleClosePnncView = renderCommercialPossibleClose(possibleCloseItems, "pnnc");
  replaceBlockPreview("(COM) Posible Cierre RCH", possibleCloseRchView.html, possibleCloseRchView.count);
  replaceBlockPreview("(COM) Posible Cierre PNNC", possibleClosePnncView.html, possibleClosePnncView.count);
};

const loadDiegoPortfolioCollections = async () => {
  const queryString = getDiegoCommercialQueryString();
  const response = await fetch(`/api/reports/fuerza-comercial-diego/cartera-recaudada?${queryString}`, { cache: "no-store" });
  if (!response.ok) throw new Error(`HTTP ${response.status}`);
  const data = await response.json();
  if (teamScope) {
    data.portfolio = (data.portfolio ?? []).filter((item) => isScopedTeamMember(item.advisor));
    data.items = (data.items ?? []).filter((item) => !item.coordinator || isTeamDepartment(item.coordinator));
  }
  const portfolioRows = data.portfolio.map((item) => `<tr data-advisor="${encodeURIComponent(item.advisor)}" data-line="${normalizeFilterText(item.commercialLine).includes("insolvencia") ? "pnnc" : normalizeFilterText(item.commercialLine)}"><td>${item.advisor}</td><td><span class="portfolio-line ${normalizeFilterText(item.commercialLine)}">${item.commercialLine}</span></td><td>${formatNumber.format(item.receivable)}</td><td>${formatNumber.format(item.withNovelty)}</td><td>${formatNumber.format(item.successful)}</td></tr>`);
  const portfolioContent = portfolioRows.length
    ? renderDataTable(["Asesor", "L&iacute;nea de negocio", "Valor cartera por cobrar", "Valor cartera con novedad", "Valor cartera exitosa"], portfolioRows, "portfolio-state-table")
    : `<div class="empty-block"><strong>Sin cartera disponible</strong><span>Sincronice las pipelines RCH Cartera e Insolvencia Cartera.</span></div>`;
  replaceBlockPreview("Estado de cartera", portfolioContent, data.portfolio.length);
  const rows = [...(data.items ?? [])]
    .sort((left, right) => {
      const monthDiff = Number.parseInt(String(left.month ?? "").slice(0, 2), 10) - Number.parseInt(String(right.month ?? "").slice(0, 2), 10);
      if (monthDiff !== 0) return monthDiff;
      const lineDiff = String(left.commercialLine ?? "").localeCompare(String(right.commercialLine ?? ""), "es", { sensitivity: "base" });
      if (lineDiff !== 0) return lineDiff;
      return String(left.coordinator ?? "").localeCompare(String(right.coordinator ?? ""), "es", { sensitivity: "base" });
    })
    .map((item) => {
      const goal = Number(item.goal ?? 0);
      const collected = Number(item.collected ?? 0);
      const compliance = goal ? percentFormatter.format(collected / goal) : "N/A";
      return `<tr data-coordinator="${encodeURIComponent(item.coordinator ?? "")}" data-line="${normalizeFilterText(item.commercialLine).includes("insolvencia") ? "pnnc" : normalizeFilterText(item.commercialLine)}">
        <td>${escapeHtml(item.month ?? "")}</td>
        <td>${escapeHtml(item.commercialLine ?? "")}</td>
        <td>${formatNumber.format(goal)}</td>
        <td>${formatNumber.format(collected)}</td>
        <td>${compliance}</td>
        <td>${escapeHtml(item.coordinator ?? "")}</td>
        <td>${escapeHtml(item.coordinatorId ?? "")}</td>
      </tr>`;
    });
  const content = rows.length
    ? renderDataTable(["Mes", "L&iacute;nea comercial", "Meta", "$ Recaudo", "%", "Coordinador", "ID Coordinador"], rows, "portfolio-collection-table")
    : `<div class="empty-block"><strong>Sin recaudos para ${data.year}</strong><span>Las pipelines de cartera aún se están sincronizando.</span></div>`;
  replaceBlockPreview("Cartera recaudada", content, data.items.length);
};

const loadDiegoLeadershipAndCommissions = async () => {
  const queryString = getDiegoCommercialQueryString();
  const response = await fetch(`/api/reports/fuerza-comercial-diego/liderazgo-comisiones?${queryString}`, { cache: "no-store" });
  if (!response.ok) throw new Error(`HTTP ${response.status}`);
  const data = await response.json();
  data.coordinatorValues = data.coordinatorValues ?? [];
  if (teamScope) {
    data.coordinatorValues = isAdvisorTeamScope()
      ? data.coordinatorValues.filter((item) => isScopedTeamMember(item.advisor))
      : isLeaderTeamScope()
        ? data.coordinatorValues.filter((item) => isTeamDepartment(item.leader) || isScopedTeamMember(item.advisor))
        : data.coordinatorValues.filter((item) => isTeamDepartment(item.coordinator));
    data.leadership = isAdvisorTeamScope()
      ? (data.leadership ?? []).filter((item) => isScopedTeamMember(item.advisor))
      : (data.leadership ?? []).filter((item) => isTeamDepartment(item.leader) || isTeamDepartment(item.coordinator));
    data.commissions = (data.commissions ?? []).filter((item) => isScopedTeamMember(item.advisor));
    data.relationships = (data.relationships ?? []).filter((item) => isScopedTeamMember(item.advisor));
  }
  coordinatorRadicatedData = data.coordinatorValues;
  const leaderRadicatedData = coordinatorRadicatedData.filter((item) => Boolean(item.leader));
  commercialHierarchy = data.relationships?.length ? data.relationships : (data.leadership ?? []);

  const coordinatorItems = coordinatorRadicatedData.filter((item) => isCoordinatorGroupName(item.coordinator ?? ""));
  const leaderItems = leaderRadicatedData.filter((item) => isLeaderGroupName(item.leader ?? ""));
  const leaderCount = new Set(leaderItems.map((item) => item.leader).filter(Boolean)).size;
  const coordinatorCount = new Set(coordinatorItems.map((item) => item.coordinator).filter(Boolean)).size;
  replaceBlockPreview("Valores radicados por líder", renderMonthlyMatrix("Líder", leaderItems, "leader", true), leaderCount);
  replaceBlockPreview("Valores radicados por coordinador", renderMonthlyMatrix("Coordinador", coordinatorItems, "coordinator", true), coordinatorCount);
  const coordinatorPerformance = renderPerformanceTable(coordinatorItems, "coordinator", true);
  const leaderPerformance = renderPerformanceTable(leaderItems, "leader");
  replaceBlockPreview("Detalle de coordinadores", coordinatorPerformance.html, coordinatorPerformance.count);
  replaceBlockPreview("Detalle de radicaciones por líder", leaderPerformance.html, leaderPerformance.count);
  replaceBlockPreview("Comisiones por asesor", data.commissions.length
    ? renderCommissionMatrix(data.commissions)
    : `<div class="empty-block"><strong>Sin comisiones para ${data.year}</strong><span>La pipeline Cuentas de Cobro no contiene registros pagados para este periodo.</span></div>`, data.commissions.length);
};

const loadDiegoFilterHierarchy = async () => {
  const response = await fetch("/api/reports/fuerza-comercial-diego/jerarquia-filtros", { cache: "no-store" });
  if (!response.ok) throw new Error(`HTTP ${response.status}`);
  const data = await response.json();
  commercialHierarchy = (data.items ?? []).filter((item) => {
    if (!teamScope) return true;
    if (isAdvisorTeamScope()) return isTeamMember(item.advisor);
    if (isLeaderTeamScope()) return isTeamDepartment(item.leader);
    return isTeamDepartment(item.coordinator);
  });
  setupDiegoFilters();
};

const normalizeFilterText = (value) => String(value ?? "").trim().toLocaleLowerCase("es-CO");
const splitFilterValues = (value) => String(value ?? "")
  .split("\u0001")
  .map((item) => normalizeFilterText(item))
  .filter(Boolean);

const multiFilterSelectIds = new Set(["diegoMonth", "diegoCoordinator", "diegoLeader", "diegoAdvisor"]);

const isMultiFilterSelect = (selectOrId) => {
  const id = typeof selectOrId === "string" ? selectOrId : selectOrId?.id;
  return multiFilterSelectIds.has(id);
};

const selectedFilterValues = (selectOrId) => {
  const select = typeof selectOrId === "string" ? document.getElementById(selectOrId) : selectOrId;
  if (!select) return [];
  if (!isMultiFilterSelect(select)) return select.value;
  return [...select.selectedOptions]
    .map((option) => option.value)
    .filter((value) => value !== "all");
};

const isFilterSelectionAll = (selected) => Array.isArray(selected)
  ? selected.length === 0
  : selected === "all";

const selectedValueMatches = (value, selected, normalizer = normalizeFilterText) => {
  if (Array.isArray(selected)) {
    if (!selected.length) return true;
    const normalizedValue = normalizer(value ?? "");
    return selected.some((item) => normalizedValue === normalizer(item ?? ""));
  }
  return selected === "all" || normalizer(value ?? "") === normalizer(selected ?? "");
};

const setFilterSelectionToAll = (id) => {
  const select = document.getElementById(id);
  if (!select) return;
  if (isMultiFilterSelect(select)) {
    [...select.options].forEach((option) => {
      option.selected = option.value === "all";
    });
  } else {
    select.value = "all";
  }
  syncSearchableSelectInput(select);
};

const hierarchySelection = () => ({
  line: document.getElementById("diegoLine").value,
  coordinator: selectedFilterValues("diegoCoordinator"),
  leader: selectedFilterValues("diegoLeader"),
  advisor: selectedFilterValues("diegoAdvisor")
});

const normalizeHierarchyLine = (value = "") => {
  const normalized = normalizeFilterText(value);
  if (normalized.includes("rch")) return "rch";
  if (normalized.includes("pnnc") || normalized.includes("insolvencia")) return "pnnc";
  return normalized;
};

const matchesHierarchySelection = (item, selection, ignored = "") => {
  const line = normalizeHierarchyLine(item.commercialLine ?? "");
  return (ignored === "line" || selection.line === "all" || line === selection.line)
    && (ignored === "coordinator" || selectedValueMatches(item.coordinator, selection.coordinator))
    && (ignored === "leader" || selectedValueMatches(item.leader, selection.leader))
    && (ignored === "advisor" || selectedValueMatches(item.advisor, selection.advisor));
};

const uniqueHierarchyValues = (field, selection, ignored) => [...new Set(commercialHierarchy
  .filter((item) => matchesHierarchySelection(item, selection, ignored))
  .map((item) => item[field])
  .filter((value) => value && !value.startsWith("Sin ")))]
  .sort((left, right) => left.localeCompare(right, "es", { sensitivity: "base" }));

const availableAdvisorValues = (selection) => {
  const hierarchyValues = uniqueHierarchyValues("advisor", selection, "advisor");
  const fallbackValues = [
    ...(generalDashboardData?.advisors ?? []).map((item) => item.advisor),
    ...(generalDashboardData?.possibleCloseCommercial ?? []).map((item) => item.advisor),
    ...coordinatorRadicatedData.map((item) => item.advisor)
  ].filter(Boolean);
  const allowedFallbackValues = teamScope
    ? fallbackValues.filter((advisor) => isScopedTeamMember(advisor))
    : fallbackValues;
  const hasHierarchyFilters = selection.line !== "all"
    || !isFilterSelectionAll(selection.coordinator)
    || !isFilterSelectionAll(selection.leader);
  const hierarchyValueKeys = new Set(hierarchyValues.map(normalizeFilterText));
  const values = [...new Set([...hierarchyValues, ...allowedFallbackValues])]
    .filter((value) => !hasHierarchyFilters || hierarchyValueKeys.has(normalizeFilterText(value)))
    .filter((value) => value && !String(value).startsWith("Sin "));
  return values.sort((left, right) => left.localeCompare(right, "es", { sensitivity: "base" }));
};

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
  const previous = isMultiFilterSelect(select) ? selectedFilterValues(select) : select.value;
  select.replaceChildren();
  select.multiple = isMultiFilterSelect(select);
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
  if (isMultiFilterSelect(select)) {
    const previousSet = new Set(previous);
    let selectedCount = 0;
    [...select.options].forEach((option) => {
      const selected = option.value !== "all" && previousSet.has(option.value);
      option.selected = selected;
      if (selected) selectedCount += 1;
    });
    allOption.selected = selectedCount === 0;
  } else if ([...select.options].some((option) => option.value === previous)) {
    select.value = previous;
  }
  enhanceSearchableFilterSelect(select);
};

const getDiegoCommercialQueryString = () => {
  const params = new URLSearchParams();
  params.set("year", document.getElementById("diegoYear")?.value ?? "2026");
  let dateFrom = commercialDateRangeTouched ? document.getElementById("diegoDateFrom")?.value : "";
  let dateTo = commercialDateRangeTouched ? document.getElementById("diegoDateTo")?.value : "";
  const months = selectedFilterValues("diegoMonth");
  if (dateFrom && dateTo && dateFrom > dateTo) [dateFrom, dateTo] = [dateTo, dateFrom];
  if (dateFrom) params.set("from", dateFrom);
  if (dateTo) params.set("to", dateTo);
  if (months.length === 1) params.set("month", months[0]);
  return params.toString();
};

const reloadDiegoCommercialData = async () => {
  await Promise.all([
    loadDiegoRadicatedValues(),
    loadDiegoDashboardData(),
    loadDiegoPortfolioCollections(),
    loadDiegoLeadershipAndCommissions()
  ]);
  diegoCommercialDataLoaded = true;
  setupDiegoFilters();
  if (reportId === "informe_general_comercial") applyGeneralCommercialLabels();
};

const parseDateInputValue = (value) => {
  if (!value) return null;
  const parts = value.split("-").map((part) => Number.parseInt(part, 10));
  if (parts.length !== 3 || parts.some((part) => !Number.isFinite(part))) return null;
  return new Date(parts[0], parts[1] - 1, parts[2]);
};

const getDiegoDateRange = () => {
  if (!commercialDateRangeTouched) return { from: null, to: null };
  const from = parseDateInputValue(document.getElementById("diegoDateFrom")?.value ?? "");
  const to = parseDateInputValue(document.getElementById("diegoDateTo")?.value ?? "");
  if (from && to && from > to) return { from: to, to: from };
  return { from, to };
};

const getMonthCodeFromText = (value = "") => {
  const match = String(value).trim().match(/^(\d{1,2})/);
  return match ? match[1].padStart(2, "0") : "";
};

const diegoMonthMatchesDateRange = (monthValue, range) => {
  if (!range.from && !range.to) return true;
  const monthNumber = Number.parseInt(getMonthCodeFromText(monthValue), 10);
  if (!Number.isFinite(monthNumber) || monthNumber < 1 || monthNumber > 12) return true;
  const year = Number.parseInt(document.getElementById("diegoYear")?.value ?? "2026", 10);
  const monthStart = new Date(year, monthNumber - 1, 1);
  const monthEnd = new Date(year, monthNumber, 0, 23, 59, 59, 999);
  return (!range.from || monthEnd >= range.from) && (!range.to || monthStart <= range.to);
};

const applyDiegoFilters = () => {
  const filters = {
    Mes: selectedFilterValues("diegoMonth"),
    "Línea comercial": document.getElementById("diegoLine").value,
    Asesor: selectedFilterValues("diegoAdvisor"),
    "Líder": selectedFilterValues("diegoLeader"),
    Coordinador: selectedFilterValues("diegoCoordinator")
  };
  const selectedLine = document.getElementById("diegoLine").value;
  const selectedMonths = selectedFilterValues("diegoMonth");
  const selectedDateRange = getDiegoDateRange();
  const monthMatchesDateRange = (monthValue) => diegoMonthMatchesDateRange(monthValue, selectedDateRange);
  const monthMatchesSelection = (monthValue) => isFilterSelectionAll(selectedMonths)
    || selectedMonths.some((month) => getMonthCodeFromText(monthValue) === month);
  const selectedPendingLeader = "all";
  const selectedHierarchy = hierarchySelection();
  const hierarchyIndexes = {
    advisors: new Set(),
    leaders: new Set(),
    coordinators: new Set(),
    advisorLeader: new Set(),
    advisorCoordinator: new Set(),
    leaderCoordinator: new Set(),
    complete: new Set()
  };
  const relatedHierarchy = commercialHierarchy.filter((item) => matchesHierarchySelection(item, selectedHierarchy));
  const hasHierarchyFilter = selectedHierarchy.line !== "all"
    || !isFilterSelectionAll(selectedHierarchy.coordinator)
    || !isFilterSelectionAll(selectedHierarchy.leader)
    || !isFilterSelectionAll(selectedHierarchy.advisor);
  relatedHierarchy.forEach((item) => {
    const advisor = normalizeFilterText(item.advisor ?? "");
    const leader = normalizeFilterText(item.leader ?? "");
    const coordinator = normalizeFilterText(item.coordinator ?? "");
    if (advisor) hierarchyIndexes.advisors.add(advisor);
    if (leader) hierarchyIndexes.leaders.add(leader);
    if (coordinator) hierarchyIndexes.coordinators.add(coordinator);
    if (advisor && leader) hierarchyIndexes.advisorLeader.add(`${advisor}\u0001${leader}`);
    if (advisor && coordinator) hierarchyIndexes.advisorCoordinator.add(`${advisor}\u0001${coordinator}`);
    if (leader && coordinator) hierarchyIndexes.leaderCoordinator.add(`${leader}\u0001${coordinator}`);
    if (advisor && leader && coordinator) hierarchyIndexes.complete.add(`${advisor}\u0001${leader}\u0001${coordinator}`);
  });

  const belongsToSelectedHierarchy = (advisorValue, leaderValue, coordinatorValue) => {
    const advisors = splitFilterValues(advisorValue);
    const leaders = splitFilterValues(leaderValue);
    const coordinators = splitFilterValues(coordinatorValue);
    if (!advisors.length && !leaders.length && !coordinators.length) return true;
    if (!commercialHierarchy.length) return true;
    if (advisors.length && leaders.length && coordinators.length) {
      return advisors.some((advisor) => leaders.some((leader) => coordinators.some((coordinator) =>
        hierarchyIndexes.complete.has(`${advisor}\u0001${leader}\u0001${coordinator}`))));
    }
    if (advisors.length && leaders.length) {
      return advisors.some((advisor) => leaders.some((leader) => hierarchyIndexes.advisorLeader.has(`${advisor}\u0001${leader}`)));
    }
    if (advisors.length && coordinators.length) {
      return advisors.some((advisor) => coordinators.some((coordinator) => hierarchyIndexes.advisorCoordinator.has(`${advisor}\u0001${coordinator}`)));
    }
    if (leaders.length && coordinators.length) {
      return leaders.some((leader) => coordinators.some((coordinator) => hierarchyIndexes.leaderCoordinator.has(`${leader}\u0001${coordinator}`)));
    }
    if (advisors.length) return advisors.some((advisor) => hierarchyIndexes.advisors.has(advisor));
    if (leaders.length) return leaders.some((leader) => hierarchyIndexes.leaders.has(leader));
    return coordinators.some((coordinator) => hierarchyIndexes.coordinators.has(coordinator));
  };

  if (generalDashboardData?.possibleCloseCommercial) {
    const possibleCloseItems = generalDashboardData.possibleCloseCommercial.filter((item) => {
      const matchesLine = selectedLine === "all" || commercialPossibleCloseLine(item).includes(selectedLine);
      const matchesTeam = !hasHierarchyFilter || belongsToSelectedHierarchy(item.advisor, item.leader, item.coordinator);
      return matchesLine && matchesTeam;
    });
    const possibleCloseRchView = renderCommercialPossibleClose(possibleCloseItems, "rch");
    const possibleClosePnncView = renderCommercialPossibleClose(possibleCloseItems, "pnnc");
    replaceBlockPreview("(COM) Posible Cierre RCH", possibleCloseRchView.html, possibleCloseRchView.count);
    replaceBlockPreview("(COM) Posible Cierre PNNC", possibleClosePnncView.html, possibleClosePnncView.count);
  }

  document.querySelectorAll(".diego-block").forEach((block) => {
    const title = normalizeFilterText(block.querySelector("h3")?.textContent ?? "");
    const belongsToRch = title.includes("rch");
    const belongsToInsolvency = title.includes("pnnc") || title.includes("insolvencia");
    block.hidden = (selectedLine === "rch" && belongsToInsolvency)
      || (selectedLine === "pnnc" && belongsToRch);

    const table = block.querySelector("table");
    if (!table) return;
    if (block.dataset.blockCode?.startsWith("commercial_possible_close")) return;
    const hasMonthlyCells = table.classList.contains("radicated-matrix") || table.classList.contains("monthly-matrix");
    if (hasMonthlyCells) {
      table.querySelectorAll("[data-month]").forEach((cell) => {
        cell.hidden = !monthMatchesSelection(cell.dataset.month)
          || !monthMatchesDateRange(cell.dataset.month);
      });
    }
    const headers = [...table.querySelectorAll("thead th")].map((header) => header.textContent.trim());
    let visibleRows = 0;

    table.querySelectorAll("tbody tr").forEach((row) => {
      const matches = Object.entries(filters).every(([headerName, selected]) => {
        if (isFilterSelectionAll(selected)) return true;
        if (table.classList.contains("performance-table") && row.dataset.group) {
          if (headerName === "Coordinador" && title.includes("coordinadores")) return selectedValueMatches(decodeURIComponent(row.dataset.group), selected);
          if (headerName === "Líder" && title.includes("líder")) return selectedValueMatches(decodeURIComponent(row.dataset.group), selected);
        }
        const index = headers.indexOf(headerName);
        if (index < 0 && normalizeFilterText(headerName).includes("linea") && row.dataset.line) {
          return normalizeFilterText(row.dataset.line).includes(selected);
        }
        if (index < 0) return true;
        const cellValue = row.children[index]?.textContent.trim() ?? "";
        if (headerName === "Mes") return monthMatchesSelection(cellValue);
        if (headerName === "Línea comercial") {
          const normalizedLine = normalizeFilterText(cellValue).includes("insolvencia") ? "pnnc" : normalizeFilterText(cellValue);
          return normalizedLine.includes(selected);
        }
        return selectedValueMatches(cellValue, selected);
      });
      const advisorIndex = headers.indexOf("Asesor");
      const leaderIndex = headers.indexOf("Líder");
      const coordinatorIndex = headers.indexOf("Coordinador");
      const rowAdvisor = row.dataset.advisor ? decodeURIComponent(row.dataset.advisor) : (advisorIndex >= 0 ? row.children[advisorIndex]?.textContent.trim() : null);
      const rowLeader = row.dataset.leader ? decodeURIComponent(row.dataset.leader) : (leaderIndex >= 0 ? row.children[leaderIndex]?.textContent.trim() : (title.includes("líder") ? row.dataset.group : null));
      const rowCoordinator = row.dataset.coordinator ? decodeURIComponent(row.dataset.coordinator) : (coordinatorIndex >= 0 ? row.children[coordinatorIndex]?.textContent.trim() : (title.includes("coordinador") ? row.dataset.group : null));
      const hasHierarchyIdentity = rowAdvisor || rowLeader || rowCoordinator;
      const matchesRelatedTeam = !hasHierarchyFilter || !hasHierarchyIdentity || belongsToSelectedHierarchy(rowAdvisor, rowLeader, rowCoordinator);
      const stageIndex = headers.findIndex((header) => normalizeFilterText(header).startsWith("etapa"));
      const stageValue = stageIndex >= 0 ? normalizeFilterText(row.children[stageIndex]?.textContent ?? "") : "";
      const rowMonthIndex = headers.indexOf("Mes");
      const matchesDateRange = rowMonthIndex < 0 || monthMatchesDateRange(row.children[rowMonthIndex]?.textContent ?? "");
      const matchesVisibleMonthCells = !hasMonthlyCells
        || [...row.querySelectorAll("[data-month]")].some((cell) => !cell.hidden && cell.textContent.trim());
      const isPendingLeader = stageValue.includes("lider") || stageValue.includes("líder");
      const matchesPendingLeader = selectedPendingLeader === "all"
        || stageIndex < 0
        || (selectedPendingLeader === "pending" ? isPendingLeader : !isPendingLeader);
      const isVisible = matches && matchesDateRange && matchesVisibleMonthCells && matchesPendingLeader && matchesRelatedTeam;
      row.hidden = !isVisible;
      if (isVisible && row.dataset.summaryRow !== "true") visibleRows += 1;
    });

    const badge = block.querySelector(".diego-block-title em");
    if (badge) badge.textContent = `${visibleRows} registros`;
    decorateTableTotals(block);
  });
  applyDiegoGridPacking();
};

const setupDiegoFilters = () => {
  const refreshHierarchyOptions = () => {
    fillFilterOptions("diegoCoordinator", uniqueHierarchyValues("coordinator", hierarchySelection(), "coordinator"));
    fillFilterOptions("diegoLeader", uniqueHierarchyValues("leader", hierarchySelection(), "leader"));
    fillFilterOptions("diegoAdvisor", availableAdvisorValues(hierarchySelection()));
  };
  refreshHierarchyOptions();
  refreshHierarchyOptions();
  const advisorSelect = document.getElementById("diegoAdvisor");
  advisorSelect.disabled = false;
  advisorSelect.title = "Filtrar por asesor";
  ["diegoMonth", "diegoCoordinator", "diegoLeader", "diegoAdvisor"].forEach((id) => {
    const select = document.getElementById(id);
    if (!select) return;
    select.multiple = true;
    if (![...select.selectedOptions].length) setFilterSelectionToAll(id);
    enhanceSearchableFilterSelect(select);
  });
  ["diegoMonth", "diegoLine", "diegoAdvisor", "diegoLeader", "diegoCoordinator"].forEach((id) => {
    const select = document.getElementById(id);
    if (select.dataset.bound === "true") return;
    select.addEventListener("change", async () => {
      if (id === "diegoMonth") {
        applyDiegoFilters();
        markCommercialViewPending();
        return;
      }
      if (["diegoLine", "diegoCoordinator", "diegoLeader", "diegoAdvisor"].includes(id)) setupDiegoFilters();
      else applyDiegoFilters();
    });
    select.dataset.bound = "true";
  });
  ["diegoDateFrom", "diegoDateTo"].forEach((id) => {
    const input = document.getElementById(id);
    if (!input || input.dataset.bound === "true") return;
    input.addEventListener("change", () => {
      commercialDateRangeTouched = true;
      applyDiegoFilters();
      markCommercialViewPending();
    });
    input.dataset.bound = "true";
  });
  applyDiegoFilters();
};

const clearDiegoFilters = async () => {
  const year = document.getElementById("diegoYear");
  const yearChanged = year.value !== "2026";
  const hadServerFilters = yearChanged
    || !isFilterSelectionAll(selectedFilterValues("diegoMonth"))
    || Boolean(document.getElementById("diegoDateFrom").value)
    || Boolean(document.getElementById("diegoDateTo").value);
  year.value = "2026";
  commercialDateRangeTouched = false;
  setFilterSelectionToAll("diegoMonth");
  document.getElementById("diegoDateFrom").value = "";
  document.getElementById("diegoDateTo").value = "";
  document.getElementById("diegoLine").value = "all";
  setFilterSelectionToAll("diegoCoordinator");
  setFilterSelectionToAll("diegoLeader");
  setFilterSelectionToAll("diegoAdvisor");
  clearFilterOptionSearches(["diegoMonth", "diegoCoordinator", "diegoLeader", "diegoAdvisor"]);
  setupDiegoFilters();
  if (hadServerFilters) markCommercialViewPending();
};

const loadGerenciaCompliance = async () => {
  const year = document.getElementById("gerenciaYear").value;
  const value = document.getElementById("gerenciaComercialCumplimiento");
  const detail = document.getElementById("gerenciaComercialDetalle");

  try {
    const response = await fetch(`/api/reports/gerencia/comercial-cumplimiento?year=${encodeURIComponent(year)}`);
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    const data = await response.json();

    value.textContent = data.compliance == null ? "0.0%" : gerenciaPercentFormatter.format(data.compliance);
    detail.textContent = `Alcanzado: $ ${gerenciaNumberFormatter.format(data.achieved ?? 0)} / Meta: $ ${gerenciaNumberFormatter.format(data.target ?? 0)}`;
  } catch (error) {
    value.textContent = "-";
    detail.textContent = `No fue posible cargar el indicador: ${error.message}`;
  }
};

const complianceTone = (value) => {
  if (value == null) return "";
  if (value >= 0.9) return "high";
  if (value >= 0.8) return "good";
  if (value >= 0.7) return "medium";
  return "low";
};

const getChronologicalGerenciaRows = () => filterRowsByGerenciaMonth([...gerenciaMonthlyRows])
  .sort((a, b) => gerenciaMonthOrder.indexOf(a.month) - gerenciaMonthOrder.indexOf(b.month));

const chartPointList = (rows, key, chartWidth, chartHeight, maxValue) => rows.map((row, index) => {
  const x = rows.length === 1 ? 0 : (index / (rows.length - 1)) * chartWidth;
  const y = chartHeight - (((row[key] ?? 0) / maxValue) * chartHeight);
  return `${x.toFixed(1)},${y.toFixed(1)}`;
}).join(" ");

const chartPoint = (rows, row, index, key, chartWidth, chartHeight, maxValue) => ({
  x: rows.length === 1 ? 0 : (index / (rows.length - 1)) * chartWidth,
  y: chartHeight - (((row[key] ?? 0) / maxValue) * chartHeight)
});

const updateGerenciaChartLegend = () => {
  document.querySelectorAll("[data-chart-series]").forEach((button) => {
    const series = button.dataset.chartSeries;
    button.classList.toggle("active", Boolean(gerenciaChartSeries[series]));
  });
};

const renderGerenciaLineChart = () => {
  const container = document.getElementById("gerenciaLineChart");
  if (!container) return;

  const rows = getChronologicalGerenciaRows();
  if (!rows.length) {
    container.innerHTML = `<div class="chart-empty">Sin datos para graficar.</div>`;
    return;
  }

  const width = 860;
  const height = 320;
  const padding = { top: 14, right: 20, bottom: 46, left: 96 };
  const chartWidth = width - padding.left - padding.right;
  const chartHeight = height - padding.top - padding.bottom;
  const maxValue = Math.max(...rows.flatMap((row) => [row.target ?? 0, row.achieved ?? 0]), 1);
  const scaleMax = Math.ceil(maxValue / 500000000) * 500000000;
  const ticks = [0, .2, .4, .6, .8, 1].map((ratio) => scaleMax * ratio);
  const metaPoints = chartPointList(rows, "target", chartWidth, chartHeight, scaleMax);
  const radicadoPoints = chartPointList(rows, "achieved", chartWidth, chartHeight, scaleMax);
  const activeSeries = [
    gerenciaChartSeries.meta ? "meta" : null,
    gerenciaChartSeries.radicado ? "radicado" : null
  ].filter(Boolean);

  updateGerenciaChartLegend();

  container.innerHTML = `
    <svg viewBox="0 0 ${width} ${height}" role="img" aria-label="Gráfica de meta contra total radicado">
      <g transform="translate(${padding.left} ${padding.top})">
        ${ticks.map((tick) => {
          const y = chartHeight - ((tick / scaleMax) * chartHeight);
          return `
            <line class="chart-grid-line" x1="0" y1="${y}" x2="${chartWidth}" y2="${y}"></line>
            <text class="chart-y-label" x="-10" y="${y + 4}" text-anchor="end">${gerenciaNumberFormatter.format(tick)}</text>
          `;
        }).join("")}
        ${gerenciaChartSeries.meta ? `<polyline class="line-meta" points="${metaPoints}"></polyline>` : ""}
        ${gerenciaChartSeries.radicado ? `<polyline class="line-radicado" points="${radicadoPoints}"></polyline>` : ""}
        ${rows.map((row, index) => {
          const meta = chartPoint(rows, row, index, "target", chartWidth, chartHeight, scaleMax);
          const radicado = chartPoint(rows, row, index, "achieved", chartWidth, chartHeight, scaleMax);
          const x = rows.length === 1 ? 0 : (index / (rows.length - 1)) * chartWidth;
          const guide = activeSeries.length === 1
            ? chartPoint(rows, row, index, activeSeries[0] === "meta" ? "target" : "achieved", chartWidth, chartHeight, scaleMax)
            : null;
          return `
            <g class="chart-hover-group">
              <rect class="chart-hover-zone" x="${x - 28}" y="0" width="56" height="${chartHeight}" tabindex="0"></rect>
              ${guide ? `<line class="chart-hover-guide" x1="${x}" y1="${guide.y}" x2="${x}" y2="${chartHeight}"></line>` : ""}
              ${gerenciaChartSeries.meta ? `<circle class="chart-point point-meta" cx="${meta.x}" cy="${meta.y}" r="4"></circle>` : ""}
              ${gerenciaChartSeries.radicado ? `<circle class="chart-point point-radicado" cx="${radicado.x}" cy="${radicado.y}" r="4"></circle>` : ""}
              <foreignObject class="chart-tooltip-box" x="${Math.min(Math.max(x + 10, 0), chartWidth - 210)}" y="0" width="210" height="132">
                <div xmlns="http://www.w3.org/1999/xhtml" class="chart-tooltip">
                  <strong>${row.month}</strong>
                  <span><i class="tooltip-meta"></i>Meta <b>${gerenciaNumberFormatter.format(row.target ?? 0)}</b></span>
                  <span><i class="tooltip-radicado"></i>Total Radicado <b>${gerenciaNumberFormatter.format(row.achieved ?? 0)}</b></span>
                  <span><i class="tooltip-total"></i>Total <b>${gerenciaNumberFormatter.format((row.target ?? 0) + (row.achieved ?? 0))}</b></span>
                </div>
              </foreignObject>
            </g>
          `;
        }).join("")}
        ${rows.map((row, index) => {
          const x = rows.length === 1 ? 0 : (index / (rows.length - 1)) * chartWidth;
          return `
            <line class="chart-x-tick" x1="${x}" y1="${chartHeight}" x2="${x}" y2="${chartHeight + 7}"></line>
            <text class="chart-x-label" x="${x}" y="${chartHeight + 26}" text-anchor="middle">${row.month}</text>
          `;
        }).join("")}
        <line class="chart-axis" x1="0" y1="${chartHeight}" x2="${chartWidth}" y2="${chartHeight}"></line>
      </g>
    </svg>
  `;
};

const setGerenciaChartSeries = (series) => {
  if (series === "meta") {
    gerenciaChartSeries = { meta: true, radicado: false };
  } else if (series === "radicado") {
    gerenciaChartSeries = { meta: false, radicado: true };
  }

  renderGerenciaLineChart();
};

const runGerenciaChartAction = (action) => {
  if (action === "all") {
    gerenciaChartSeries = { meta: true, radicado: true };
  } else if (action === "invert") {
    gerenciaChartSeries = {
      meta: !gerenciaChartSeries.meta,
      radicado: !gerenciaChartSeries.radicado
    };

    if (!gerenciaChartSeries.meta && !gerenciaChartSeries.radicado) {
      gerenciaChartSeries = { meta: true, radicado: true };
    }
  }

  renderGerenciaLineChart();
};

const renderGerenciaAverageChart = () => {
  const container = document.getElementById("gerenciaAverageChart");
  if (!container) return;

  const rows = getChronologicalGerenciaRows().filter((row) => row.compliance != null);
  if (!rows.length) {
    container.innerHTML = `<div class="chart-empty">Sin datos para graficar.</div>`;
    return;
  }

  const average = rows.reduce((sum, row) => sum + row.compliance, 0) / rows.length;
  const percent = gerenciaPercentFormatter.format(average);
  container.innerHTML = `
    <div class="average-visual">
      <div class="average-icon" aria-hidden="true"><i></i><i></i><i></i></div>
      <strong>${percent}</strong>
      <span>Promedio mensual acumulado</span>
    </div>
  `;
};

const renderGerenciaCharts = () => {
  document.getElementById("gerenciaLineChartTitle").textContent = `(GER) Gráfica Cumplimiento Comercial General ${gerenciaMonthlySummary.year}`;
  document.getElementById("gerenciaAverageChartTitle").textContent = `(GER) Porcentaje promedio acumulado Comercial General ${gerenciaMonthlySummary.year}`;
  renderGerenciaLineChart();
  renderGerenciaAverageChart();
};

const updatePnncChartLegend = () => {
  document.querySelectorAll("[data-pnnc-chart-series]").forEach((button) => {
    const series = button.dataset.pnncChartSeries;
    button.classList.toggle("active", Boolean(pnncChartSeries[series]));
  });
};

const renderPnncDetailChart = () => {
  const container = document.getElementById("pnncDetailChart");
  if (!container) return;

  const rows = filterRowsByGerenciaMonth([...pnncDetailRows]).sort((a, b) => gerenciaMonthOrder.indexOf(a.month) - gerenciaMonthOrder.indexOf(b.month));
  if (!rows.length) {
    container.innerHTML = `<div class="chart-empty">Sin datos para graficar.</div>`;
    return;
  }

  const width = 860;
  const height = 360;
  const padding = { top: 14, right: 20, bottom: 46, left: 96 };
  const chartWidth = width - padding.left - padding.right;
  const chartHeight = height - padding.top - padding.bottom;
  const maxValue = Math.max(...rows.flatMap((row) => [row.target ?? 0, row.achieved ?? 0]), 1);
  const scaleMax = Math.ceil(maxValue / 300000000) * 300000000;
  const ticks = [0, .2, .4, .6, .8, 1].map((ratio) => scaleMax * ratio);
  const metaPoints = chartPointList(rows, "target", chartWidth, chartHeight, scaleMax);
  const radicadoPoints = chartPointList(rows, "achieved", chartWidth, chartHeight, scaleMax);

  updatePnncChartLegend();
  container.innerHTML = `
    <svg viewBox="0 0 ${width} ${height}" role="img" aria-label="Gráfica detalle PNNC">
      <g transform="translate(${padding.left} ${padding.top})">
        ${ticks.map((tick) => {
          const y = chartHeight - ((tick / scaleMax) * chartHeight);
          return `
            <line class="chart-grid-line" x1="0" y1="${y}" x2="${chartWidth}" y2="${y}"></line>
            <text class="chart-y-label" x="-10" y="${y + 4}" text-anchor="end">${gerenciaNumberFormatter.format(tick)}</text>
          `;
        }).join("")}
        ${pnncChartSeries.meta ? `<polyline class="line-meta" points="${metaPoints}"></polyline>` : ""}
        ${pnncChartSeries.radicado ? `<polyline class="line-radicado" points="${radicadoPoints}"></polyline>` : ""}
        ${rows.map((row, index) => {
          const meta = chartPoint(rows, row, index, "target", chartWidth, chartHeight, scaleMax);
          const radicado = chartPoint(rows, row, index, "achieved", chartWidth, chartHeight, scaleMax);
          const x = rows.length === 1 ? 0 : (index / (rows.length - 1)) * chartWidth;
          return `
            <g class="chart-hover-group">
              <rect class="chart-hover-zone" x="${x - 28}" y="0" width="56" height="${chartHeight}" tabindex="0"></rect>
              ${pnncChartSeries.meta ? `<circle class="chart-point point-meta" cx="${meta.x}" cy="${meta.y}" r="4"></circle>` : ""}
              ${pnncChartSeries.radicado ? `<circle class="chart-point point-radicado" cx="${radicado.x}" cy="${radicado.y}" r="4"></circle>` : ""}
              <foreignObject class="chart-tooltip-box" x="${Math.min(Math.max(x + 10, 0), chartWidth - 230)}" y="0" width="230" height="132">
                <div xmlns="http://www.w3.org/1999/xhtml" class="chart-tooltip">
                  <strong>${row.month}</strong>
                  <span><i class="tooltip-meta"></i>Meta PNNC <b>${gerenciaNumberFormatter.format(row.target ?? 0)}</b></span>
                  <span><i class="tooltip-radicado"></i>Total Radicado <b>${gerenciaNumberFormatter.format(row.achieved ?? 0)}</b></span>
                  <span><i class="tooltip-total"></i>Total <b>${gerenciaNumberFormatter.format((row.target ?? 0) + (row.achieved ?? 0))}</b></span>
                </div>
              </foreignObject>
            </g>
          `;
        }).join("")}
        ${rows.map((row, index) => {
          const x = rows.length === 1 ? 0 : (index / (rows.length - 1)) * chartWidth;
          return `
            <line class="chart-x-tick" x1="${x}" y1="${chartHeight}" x2="${x}" y2="${chartHeight + 7}"></line>
            <text class="chart-x-label" x="${x}" y="${chartHeight + 26}" text-anchor="middle">${row.month}</text>
          `;
        }).join("")}
        <line class="chart-axis" x1="0" y1="${chartHeight}" x2="${chartWidth}" y2="${chartHeight}"></line>
      </g>
    </svg>
  `;
};

const setPnncChartSeries = (series) => {
  pnncChartSeries = series === "meta"
    ? { meta: true, radicado: false }
    : { meta: false, radicado: true };
  renderPnncDetailChart();
};

const runPnncChartAction = (action) => {
  if (action === "all") {
    pnncChartSeries = { meta: true, radicado: true };
  } else if (action === "invert") {
    pnncChartSeries = { meta: !pnncChartSeries.meta, radicado: !pnncChartSeries.radicado };
    if (!pnncChartSeries.meta && !pnncChartSeries.radicado) {
      pnncChartSeries = { meta: true, radicado: true };
    }
  }
  renderPnncDetailChart();
};

function comparePnncDetailRows(a, b) {
  const direction = pnncDetailSort.direction === "asc" ? 1 : -1;
  let left = a[pnncDetailSort.key];
  let right = b[pnncDetailSort.key];

  if (pnncDetailSort.key === "month") {
    left = gerenciaMonthOrder.indexOf(a.month);
    right = gerenciaMonthOrder.indexOf(b.month);
  }

  left ??= -Infinity;
  right ??= -Infinity;

  if (left === right) return 0;
  return left > right ? direction : -direction;
}

function updatePnncSortHeaders() {
  document.querySelectorAll("[data-pnnc-sort]").forEach((button) => {
    button.classList.remove("asc", "desc");
    button.setAttribute("aria-sort", "none");

    if (button.dataset.pnncSort === pnncDetailSort.key && pnncDetailSort.direction) {
      button.classList.add(pnncDetailSort.direction);
      button.setAttribute("aria-sort", pnncDetailSort.direction === "asc" ? "ascending" : "descending");
    }
  });
}

const renderPnncDetail = () => {
  const tbody = document.getElementById("pnncDetailRows");
  const baseRows = filterRowsByGerenciaMonth(pnncDetailRows);
  const rows = pnncDetailSort.direction
    ? [...baseRows].sort(comparePnncDetailRows)
    : baseRows;
  const filteredTotals = getFilteredTotals(baseRows, ["achieved"]);
  const filteredCompliance = baseRows.reduce((sum, row) => sum + (row.compliance ?? 0), 0);

  updatePnncSortHeaders();
  document.getElementById("pnncDetailTableTitle").textContent = `(GER) Detalle Cumplimiento PNNC ${pnncDetailSummary.year}`;
  document.getElementById("pnncDetailChartTitle").textContent = `(GER) Gráfica Detalle Cumplimiento PNNC ${pnncDetailSummary.year}`;
  document.getElementById("pnncDetailTotalAchieved").textContent = gerenciaNumberFormatter.format(hasGerenciaMonthFilter() ? filteredTotals.achieved : (pnncDetailSummary.totalAchieved ?? 0));
  document.getElementById("pnncDetailTotalCompliance").textContent = gerenciaPercentFormatter.format(hasGerenciaMonthFilter() ? filteredCompliance : (pnncDetailSummary.complianceSummary ?? 0));

  if (!rows.length) {
    tbody.innerHTML = `<tr><td colspan="4">Sin registros PNNC.</td></tr>`;
    renderPnncDetailChart();
    return;
  }

  tbody.innerHTML = rows.map((row) => `
    <tr>
      <td>${row.month}</td>
      <td>${gerenciaNumberFormatter.format(row.target ?? 0)}</td>
      <td>${gerenciaNumberFormatter.format(row.achieved ?? 0)}</td>
      <td class="compliance-cell ${complianceTone(row.compliance)}">${row.compliance == null ? "" : gerenciaPercentFormatter.format(row.compliance)}</td>
    </tr>
  `).join("");
  renderPnncDetailChart();
};

const cyclePnncSort = (key) => {
  if (pnncDetailSort.key !== key) {
    pnncDetailSort = { key, direction: "desc" };
  } else if (pnncDetailSort.direction === "desc") {
    pnncDetailSort.direction = "asc";
  } else if (pnncDetailSort.direction === "asc") {
    pnncDetailSort.direction = null;
  } else {
    pnncDetailSort.direction = "desc";
  }

  renderPnncDetail();
};

const compareGerenciaRows = (a, b) => {
  const direction = gerenciaSort.direction === "asc" ? 1 : -1;
  let left = a[gerenciaSort.key];
  let right = b[gerenciaSort.key];

  if (gerenciaSort.key === "month") {
    left = gerenciaMonthOrder.indexOf(a.month);
    right = gerenciaMonthOrder.indexOf(b.month);
  }

  left ??= -Infinity;
  right ??= -Infinity;

  if (left === right) return 0;
  return left > right ? direction : -direction;
};

const updateGerenciaSortHeaders = () => {
  document.querySelectorAll("[data-gerencia-sort]").forEach((button) => {
    button.classList.remove("asc", "desc");
    button.setAttribute("aria-sort", "none");

    if (button.dataset.gerenciaSort === gerenciaSort.key && gerenciaSort.direction) {
      button.classList.add(gerenciaSort.direction);
      button.setAttribute("aria-sort", gerenciaSort.direction === "asc" ? "ascending" : "descending");
    }
  });
};

const renderGerenciaMonthlyRows = () => {
  const tbody = document.getElementById("gerenciaComercialMensualRows");
  const baseRows = filterRowsByGerenciaMonth(gerenciaMonthlyRows);
  const rows = gerenciaSort.direction
    ? [...baseRows].sort(compareGerenciaRows)
    : baseRows;
  const filteredTotals = getFilteredTotals(baseRows, ["target", "achieved"]);
  const filteredCompliance = baseRows.reduce((sum, row) => sum + (row.compliance ?? 0), 0);
  const hasMonthFilter = hasGerenciaMonthFilter();

  updateGerenciaSortHeaders();
  document.querySelector(".gerencia-table-card h3").textContent = `(GER) Cumplimiento Comercial General ${gerenciaMonthlySummary.year}`;
  document.getElementById("gerenciaMetaTotal").textContent = "";
  document.getElementById("gerenciaRadicadoTotal").textContent = gerenciaNumberFormatter.format(hasMonthFilter ? filteredTotals.achieved : (gerenciaMonthlySummary.totalAchieved ?? 0));
  document.getElementById("gerenciaCumplimientoTotal").textContent = gerenciaPercentFormatter.format(hasMonthFilter ? filteredCompliance : (gerenciaMonthlySummary.totalCompliance ?? 0));
  const kpiValue = document.getElementById("gerenciaComercialCumplimiento");
  const kpiDetail = document.getElementById("gerenciaComercialDetalle");
  if (hasMonthFilter && kpiValue && kpiDetail) {
    const compliance = filteredTotals.target ? filteredTotals.achieved / filteredTotals.target : null;
    kpiValue.textContent = compliance == null ? "0.0%" : gerenciaPercentFormatter.format(compliance);
    kpiDetail.textContent = baseRows.length
      ? `${getGerenciaSelectedMonthLabel()}: $ ${gerenciaNumberFormatter.format(filteredTotals.achieved ?? 0)} / $ ${gerenciaNumberFormatter.format(filteredTotals.target ?? 0)}`
      : `Sin datos para ${getGerenciaSelectedMonthLabel()}`;
  }
  renderGerenciaCharts();

  if (!rows.length) {
    tbody.innerHTML = `<tr><td colspan="4">Sin registros para ${gerenciaMonthlySummary.year}. Verifique sincronización y filtros.</td></tr>`;
    return;
  }

  tbody.innerHTML = rows.map((row) => `
    <tr>
      <td>${row.month}</td>
      <td>${gerenciaNumberFormatter.format(row.target ?? 0)}</td>
      <td>${gerenciaNumberFormatter.format(row.achieved ?? 0)}</td>
      <td class="compliance-cell ${complianceTone(row.compliance)}">${row.compliance == null ? "" : gerenciaPercentFormatter.format(row.compliance)}</td>
    </tr>
  `).join("");
};

const cycleGerenciaSort = (key) => {
  if (gerenciaSort.key !== key) {
    gerenciaSort = { key, direction: "desc" };
  } else if (gerenciaSort.direction === "desc") {
    gerenciaSort.direction = "asc";
  } else if (gerenciaSort.direction === "asc") {
    gerenciaSort.direction = null;
  } else {
    gerenciaSort.direction = "desc";
  }

  renderGerenciaMonthlyRows();
};

const loadGerenciaMonthlyCompliance = async () => {
  const year = document.getElementById("gerenciaYear").value;
  const tbody = document.getElementById("gerenciaComercialMensualRows");

  try {
    const response = await fetch(`/api/reports/gerencia/comercial-cumplimiento-mensual?year=${encodeURIComponent(year)}`);
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    const data = await response.json();
    const complianceSummary = (data.rows ?? []).reduce((sum, row) => sum + (row.compliance ?? 0), 0);

    gerenciaMonthlyRows = data.rows ?? [];
    gerenciaMonthlySummary = {
      year: data.year,
      totalAchieved: data.totalAchieved,
      totalCompliance: complianceSummary
    };
    renderGerenciaMonthlyRows();
  } catch (error) {
    tbody.innerHTML = `<tr><td colspan="4">No fue posible cargar la tabla: ${error.message}</td></tr>`;
  }
};

const renderPossibleCloseAmount = (value) => {
  if (!value) return "";
  return gerenciaNumberFormatter.format(value);
};

const loadGerenciaPossibleClose = async () => {
  const tbody = document.getElementById("possibleCloseRows");

  try {
    const response = await fetch("/api/reports/gerencia/posible-cierre");
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    const data = await response.json();

    if (!data.rows?.length) {
      tbody.innerHTML = `<tr><td colspan="5">Sin registros para posible cierre.</td></tr>`;
      return;
    }

    tbody.innerHTML = data.rows.map((row) => `
      <tr>
        <td colspan="2">${row.stage}</td>
        <td>${renderPossibleCloseAmount(row.amount1116)}</td>
        <td>${renderPossibleCloseAmount(row.amountPnnc)}</td>
        <td>${renderPossibleCloseAmount(row.amountRch)}</td>
      </tr>
    `).join("");

    document.getElementById("possibleCloseTotal1116").textContent = renderPossibleCloseAmount(data.totals?.amount1116);
    document.getElementById("possibleCloseTotalPnnc").textContent = renderPossibleCloseAmount(data.totals?.amountPnnc);
    document.getElementById("possibleCloseTotalRch").textContent = renderPossibleCloseAmount(data.totals?.amountRch);
  } catch (error) {
    tbody.innerHTML = `<tr><td colspan="5">No fue posible cargar posible cierre: ${error.message}</td></tr>`;
  }
};

const loadPnncDetailCompliance = async () => {
  const year = document.getElementById("gerenciaYear").value;
  const tbody = document.getElementById("pnncDetailRows");

  try {
    const response = await fetch(`/api/reports/gerencia/detalle-pnnc?year=${encodeURIComponent(year)}`);
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    const data = await response.json();
    pnncDetailRows = data.rows ?? [];
    pnncDetailSummary = {
      year: data.year,
      totalAchieved: data.totalAchieved,
      complianceSummary: pnncDetailRows.reduce((sum, row) => sum + (row.compliance ?? 0), 0)
    };
    renderPnncDetail();
  } catch (error) {
    tbody.innerHTML = `<tr><td colspan="4">No fue posible cargar PNNC: ${error.message}</td></tr>`;
  }
};

const loadRchAccumulatedAverage = async () => {
  const year = document.getElementById("gerenciaYear").value;
  const title = document.getElementById("rchAverageTitle");
  const value = document.getElementById("rchAverageValue");

  title.textContent = `(GER) Promedio acumulado RCH ${year}`;

  try {
    const response = await fetch(`/api/reports/gerencia/promedio-rch?year=${encodeURIComponent(year)}`);
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    const data = await response.json();
    value.textContent = data.average == null ? "0.0%" : gerenciaPercentFormatter.format(data.average);
  } catch (error) {
    value.textContent = "-";
    title.textContent = `(GER) Promedio acumulado RCH ${year}`;
  }
};

const compareOperativaRchRows = (a, b) => {
  const direction = operativaRchSort.direction === "asc" ? 1 : -1;
  let left = a[operativaRchSort.key];
  let right = b[operativaRchSort.key];

  if (operativaRchSort.key === "month") {
    left = gerenciaMonthOrder.indexOf(a.month);
    right = gerenciaMonthOrder.indexOf(b.month);
  }

  left ??= -Infinity;
  right ??= -Infinity;

  if (left === right) return 0;
  return left > right ? direction : -direction;
};

const updateOperativaRchSortHeaders = () => {
  document.querySelectorAll("[data-rch-operativa-sort]").forEach((button) => {
    button.classList.remove("asc", "desc");
    button.setAttribute("aria-sort", "none");

    if (button.dataset.rchOperativaSort === operativaRchSort.key && operativaRchSort.direction) {
      button.classList.add(operativaRchSort.direction);
      button.setAttribute("aria-sort", operativaRchSort.direction === "asc" ? "ascending" : "descending");
    }
  });
};

const updateOperativaRchChartLegend = () => {
  document.querySelectorAll("[data-rch-operativa-chart-series]").forEach((button) => {
    const series = button.dataset.rchOperativaChartSeries;
    button.classList.toggle("active", Boolean(operativaRchChartSeries[series]));
  });
};

const renderOperativaRchChart = () => {
  const container = document.getElementById("operativaRchChart");
  if (!container) return;

  const rows = filterRowsByGerenciaMonth([...operativaRchRows]).sort((a, b) => gerenciaMonthOrder.indexOf(a.month) - gerenciaMonthOrder.indexOf(b.month));
  if (!rows.length) {
    container.innerHTML = `<div class="chart-empty">Sin datos para graficar.</div>`;
    return;
  }

  const width = 860;
  const height = 320;
  const padding = { top: 14, right: 20, bottom: 46, left: 54 };
  const chartWidth = width - padding.left - padding.right;
  const chartHeight = height - padding.top - padding.bottom;
  const maxValue = Math.max(...rows.flatMap((row) => [row.started ?? 0, row.finished ?? 0]), 1);
  const scaleMax = Math.max(10, Math.ceil(maxValue / 50) * 50);
  const ticks = [0, .2, .4, .6, .8, 1].map((ratio) => scaleMax * ratio);
  const startedPoints = chartPointList(rows, "started", chartWidth, chartHeight, scaleMax);
  const finishedPoints = chartPointList(rows, "finished", chartWidth, chartHeight, scaleMax);
  const activeSeries = [
    operativaRchChartSeries.started ? "started" : null,
    operativaRchChartSeries.finished ? "finished" : null
  ].filter(Boolean);

  updateOperativaRchChartLegend();

  container.innerHTML = `
    <svg viewBox="0 0 ${width} ${height}" role="img" aria-label="Gráfica procesos RCH">
      <g transform="translate(${padding.left} ${padding.top})">
        ${ticks.map((tick) => {
          const y = chartHeight - ((tick / scaleMax) * chartHeight);
          return `
            <line class="chart-grid-line" x1="0" y1="${y}" x2="${chartWidth}" y2="${y}"></line>
            <text class="chart-y-label" x="-10" y="${y + 4}" text-anchor="end">${gerenciaNumberFormatter.format(tick)}</text>
          `;
        }).join("")}
        ${operativaRchChartSeries.started ? `<polyline class="line-meta" points="${startedPoints}"></polyline>` : ""}
        ${operativaRchChartSeries.finished ? `<polyline class="line-radicado" points="${finishedPoints}"></polyline>` : ""}
        ${rows.map((row, index) => {
          const started = chartPoint(rows, row, index, "started", chartWidth, chartHeight, scaleMax);
          const finished = chartPoint(rows, row, index, "finished", chartWidth, chartHeight, scaleMax);
          const x = rows.length === 1 ? 0 : (index / (rows.length - 1)) * chartWidth;
          const guide = activeSeries.length === 1
            ? chartPoint(rows, row, index, activeSeries[0], chartWidth, chartHeight, scaleMax)
            : null;
          return `
            <g class="chart-hover-group">
              <rect class="chart-hover-zone" x="${x - 28}" y="0" width="56" height="${chartHeight}" tabindex="0"></rect>
              ${guide ? `<line class="chart-hover-guide" x1="${x}" y1="${guide.y}" x2="${x}" y2="${chartHeight}"></line>` : ""}
              ${operativaRchChartSeries.started ? `<circle class="chart-point point-meta" cx="${started.x}" cy="${started.y}" r="4"></circle>` : ""}
              ${operativaRchChartSeries.finished ? `<circle class="chart-point point-radicado" cx="${finished.x}" cy="${finished.y}" r="4"></circle>` : ""}
              <foreignObject class="chart-tooltip-box" x="${Math.min(Math.max(x + 10, 0), chartWidth - 230)}" y="0" width="230" height="132">
                <div xmlns="http://www.w3.org/1999/xhtml" class="chart-tooltip">
                  <strong>${row.month}</strong>
                  <span><i class="tooltip-meta"></i>Casos Iniciados <b>${gerenciaNumberFormatter.format(row.started ?? 0)}</b></span>
                  <span><i class="tooltip-radicado"></i>Casos Finalizados <b>${gerenciaNumberFormatter.format(row.finished ?? 0)}</b></span>
                  <span><i class="tooltip-total"></i>Total <b>${gerenciaNumberFormatter.format((row.started ?? 0) + (row.finished ?? 0))}</b></span>
                </div>
              </foreignObject>
            </g>
          `;
        }).join("")}
        ${rows.map((row, index) => {
          const x = rows.length === 1 ? 0 : (index / (rows.length - 1)) * chartWidth;
          return `
            <line class="chart-x-tick" x1="${x}" y1="${chartHeight}" x2="${x}" y2="${chartHeight + 7}"></line>
            <text class="chart-x-label" x="${x}" y="${chartHeight + 26}" text-anchor="middle">${row.month}</text>
          `;
        }).join("")}
        <line class="chart-axis" x1="0" y1="${chartHeight}" x2="${chartWidth}" y2="${chartHeight}"></line>
      </g>
    </svg>
  `;
};

const operativaRchBankColors = [
  "#23b2cf",
  "#4f5c92",
  "#60c58b",
  "#ff7e47",
  "#6e6f72",
  "#e43f5a",
  "#7c69d7",
  "#21a67a",
  "#c18f29",
  "#8aa0b5",
  "#d96ea8",
  "#43b0a5",
  "#9a8b7a",
  "#7f56d9",
  "#64748b"
];

const getOperativaRchBankTotals = () => {
  const totals = new Map();
  filterRowsByGerenciaMonth(operativaRchBankRows).forEach((row) => {
    const bank = row.bank || "SIN DEFINIR";
    totals.set(bank, (totals.get(bank) ?? 0) + (row.started ?? 0));
  });

  return [...totals.entries()]
    .map(([bank, started], index) => ({
      bank,
      started,
      color: operativaRchBankColors[index % operativaRchBankColors.length]
    }))
    .sort((a, b) => b.started - a.started);
};

const syncOperativaRchActiveBanks = (banks) => {
  const names = banks.map((item) => item.bank);
  const valid = new Set(names);
  operativaRchActiveBanks = new Set([...operativaRchActiveBanks].filter((bank) => valid.has(bank)));

  if (!operativaRchActiveBanks.size && names.length) {
    operativaRchActiveBanks = new Set(names);
  }
};

const bankDonutPoint = (centerX, centerY, radius, angle) => {
  const radians = (angle - 90) * Math.PI / 180;
  return {
    x: centerX + (radius * Math.cos(radians)),
    y: centerY + (radius * Math.sin(radians))
  };
};

const bankDonutPath = (centerX, centerY, outerRadius, innerRadius, startAngle, endAngle) => {
  const outerStart = bankDonutPoint(centerX, centerY, outerRadius, startAngle);
  const outerEnd = bankDonutPoint(centerX, centerY, outerRadius, endAngle);
  const innerEnd = bankDonutPoint(centerX, centerY, innerRadius, endAngle);
  const innerStart = bankDonutPoint(centerX, centerY, innerRadius, startAngle);
  const largeArc = endAngle - startAngle > 180 ? 1 : 0;

  return [
    `M ${outerStart.x.toFixed(2)} ${outerStart.y.toFixed(2)}`,
    `A ${outerRadius} ${outerRadius} 0 ${largeArc} 1 ${outerEnd.x.toFixed(2)} ${outerEnd.y.toFixed(2)}`,
    `L ${innerEnd.x.toFixed(2)} ${innerEnd.y.toFixed(2)}`,
    `A ${innerRadius} ${innerRadius} 0 ${largeArc} 0 ${innerStart.x.toFixed(2)} ${innerStart.y.toFixed(2)}`,
    "Z"
  ].join(" ");
};

const attachOperativaRchBankTooltip = (container, items, total) => {
  const tooltip = container.querySelector(".bank-donut-html-tooltip");
  if (!tooltip) return;

  const showTooltip = (item) => {
    const percent = total ? item.started / total : 0;
    tooltip.innerHTML = `
      <strong>${escapeHtml(item.bank)}</strong>
      <span>SUM(# Casos Iniciados)</span>
      <b>${gerenciaNumberFormatter.format(item.started)} &nbsp; ${gerenciaPercentFormatter.format(percent)}</b>
    `;
    tooltip.classList.add("visible");
    tooltip.setAttribute("aria-hidden", "false");
  };

  const hideTooltip = () => {
    tooltip.classList.remove("visible");
    tooltip.setAttribute("aria-hidden", "true");
  };

  container.querySelectorAll("[data-bank-index]").forEach((segment) => {
    const item = items[Number(segment.dataset.bankIndex)];
    if (!item) return;

    segment.addEventListener("mouseenter", () => showTooltip(item));
    segment.addEventListener("focus", () => showTooltip(item));
    segment.addEventListener("mouseleave", hideTooltip);
    segment.addEventListener("blur", hideTooltip);
  });
};

const renderOperativaRchBankLegend = (banks) => {
  const legend = document.getElementById("operativaRchBankLegend");
  if (!legend) return;

  const pageSize = 4;
  const totalPages = Math.max(1, Math.ceil(banks.length / pageSize));
  operativaRchBankPage = Math.min(Math.max(operativaRchBankPage, 0), totalPages - 1);
  const pageItems = banks.slice(operativaRchBankPage * pageSize, (operativaRchBankPage + 1) * pageSize);

  legend.innerHTML = `
    <div class="bank-legend-series">
      ${pageItems.map((item) => `
        <button class="bank-toggle ${operativaRchActiveBanks.has(item.bank) ? "active" : ""}" type="button" data-rch-bank-toggle="${escapeHtml(item.bank)}">
          <i style="--bank-color: ${item.color}"></i>${escapeHtml(item.bank)}
        </button>
      `).join("")}
    </div>
    <div class="bank-legend-actions">
      <button class="bank-page-button" type="button" data-rch-bank-action="prev" aria-label="Anterior">&#9664;</button>
      <span>${operativaRchBankPage + 1}/${totalPages}</span>
      <button class="bank-page-button" type="button" data-rch-bank-action="next" aria-label="Siguiente">&#9654;</button>
      <button class="legend-chip" type="button" data-rch-bank-action="all">All</button>
      <button class="legend-chip" type="button" data-rch-bank-action="invert">Inv</button>
    </div>
  `;
};

const renderOperativaRchBankChart = () => {
  const title = document.getElementById("operativaRchBankTitle");
  const container = document.getElementById("operativaRchBankChart");
  if (!container) return;

  if (title) title.textContent = `(GER) Procesos iniciados por banco ${operativaRchSummary.year}`;

  const banks = getOperativaRchBankTotals();
  syncOperativaRchActiveBanks(banks);
  renderOperativaRchBankLegend(banks);

  const activeBanks = banks.filter((item) => operativaRchActiveBanks.has(item.bank) && item.started > 0);
  const total = activeBanks.reduce((sum, item) => sum + item.started, 0);

  if (!activeBanks.length || !total) {
    container.innerHTML = `<div class="chart-empty">Sin datos para graficar.</div>`;
    return;
  }

  const width = 820;
  const height = 620;
  const centerX = 410;
  const centerY = 350;
  const outerRadius = 210;
  const innerRadius = 116;
  let currentAngle = 0;

  container.innerHTML = `
    <svg viewBox="0 0 ${width} ${height}" role="img" aria-label="Procesos iniciados por banco">
      ${activeBanks.map((item, index) => {
        const angle = (item.started / total) * 360;
        const startAngle = currentAngle;
        const endAngle = currentAngle + Math.min(angle, 359.99);
        const midAngle = startAngle + (angle / 2);
        currentAngle += angle;
        const labelPoint = bankDonutPoint(centerX, centerY, outerRadius + 45, midAngle);
        const percent = item.started / total;

        return `
          <g class="bank-donut-segment-group">
            <path class="bank-donut-segment" d="${bankDonutPath(centerX, centerY, outerRadius, innerRadius, startAngle, endAngle)}" fill="${item.color}" tabindex="0" data-bank-index="${index}"></path>
            ${percent >= 0.08 ? `<text class="bank-donut-label" x="${labelPoint.x.toFixed(1)}" y="${labelPoint.y.toFixed(1)}" text-anchor="${labelPoint.x >= centerX ? "start" : "end"}">${escapeHtml(item.bank)}</text>` : ""}
          </g>
        `;
      }).join("")}
      <text class="bank-donut-total" x="${centerX}" y="${centerY + 8}" text-anchor="middle">Total: ${gerenciaNumberFormatter.format(total)}</text>
    </svg>
    <div class="bank-donut-html-tooltip" aria-hidden="true"></div>
  `;

  attachOperativaRchBankTooltip(container, activeBanks, total);
};

const renderOperativaRchProcesses = () => {
  const tbody = document.getElementById("operativaRchRows");
  const baseRows = filterRowsByGerenciaMonth(operativaRchRows);
  const rows = operativaRchSort.direction
    ? [...baseRows].sort(compareOperativaRchRows)
    : baseRows;
  const totals = getFilteredTotals(baseRows, ["started", "finished"]);

  updateOperativaRchSortHeaders();
  document.getElementById("operativaRchYearBadge").textContent = operativaRchSummary.year;
  document.getElementById("operativaRchTableTitle").textContent = `(GER) Procesos Iniciados y Finalizados RCH ${operativaRchSummary.year}`;
  document.getElementById("operativaRchChartTitle").textContent = `(GER) Gráfica Procesos Iniciados y Finalizados RCH ${operativaRchSummary.year}`;
  document.getElementById("operativaRchTotalStarted").textContent = gerenciaNumberFormatter.format(hasGerenciaMonthFilter() ? totals.started : (operativaRchSummary.totalStarted ?? 0));
  document.getElementById("operativaRchTotalFinished").textContent = gerenciaNumberFormatter.format(hasGerenciaMonthFilter() ? totals.finished : (operativaRchSummary.totalFinished ?? 0));

  if (!rows.length) {
    tbody.innerHTML = `<tr><td colspan="3">Sin registros RCH Operativa.</td></tr>`;
    renderOperativaRchChart();
    renderOperativaRchBankChart();
    return;
  }

  tbody.innerHTML = rows.map((row) => `
    <tr>
      <td>${row.month}</td>
      <td>${gerenciaNumberFormatter.format(row.started ?? 0)}</td>
      <td>${gerenciaNumberFormatter.format(row.finished ?? 0)}</td>
    </tr>
  `).join("");
  renderOperativaRchChart();
  renderOperativaRchBankChart();
};

const cycleOperativaRchSort = (key) => {
  if (operativaRchSort.key !== key) {
    operativaRchSort = { key, direction: "desc" };
  } else if (operativaRchSort.direction === "desc") {
    operativaRchSort.direction = "asc";
  } else if (operativaRchSort.direction === "asc") {
    operativaRchSort.direction = null;
  } else {
    operativaRchSort.direction = "desc";
  }

  renderOperativaRchProcesses();
};

const setOperativaRchChartSeries = (series) => {
  operativaRchChartSeries = series === "started"
    ? { started: true, finished: false }
    : { started: false, finished: true };
  renderOperativaRchChart();
};

const runOperativaRchChartAction = (action) => {
  if (action === "all") {
    operativaRchChartSeries = { started: true, finished: true };
  } else if (action === "invert") {
    operativaRchChartSeries = {
      started: !operativaRchChartSeries.started,
      finished: !operativaRchChartSeries.finished
    };

    if (!operativaRchChartSeries.started && !operativaRchChartSeries.finished) {
      operativaRchChartSeries = { started: true, finished: true };
    }
  }

  renderOperativaRchChart();
};

const handleOperativaRchBankLegendClick = (event) => {
  const button = event.target.closest("button");
  if (!button) return;

  const banks = getOperativaRchBankTotals();
  const bankNames = banks.map((item) => item.bank);
  const pageSize = 4;
  const totalPages = Math.max(1, Math.ceil(banks.length / pageSize));

  if (button.dataset.rchBankToggle) {
    const bank = button.dataset.rchBankToggle;
    if (operativaRchActiveBanks.has(bank)) {
      operativaRchActiveBanks.delete(bank);
    } else {
      operativaRchActiveBanks.add(bank);
    }
  }

  if (button.dataset.rchBankAction === "prev") {
    operativaRchBankPage = (operativaRchBankPage + totalPages - 1) % totalPages;
  } else if (button.dataset.rchBankAction === "next") {
    operativaRchBankPage = (operativaRchBankPage + 1) % totalPages;
  } else if (button.dataset.rchBankAction === "all") {
    operativaRchActiveBanks = new Set(bankNames);
  } else if (button.dataset.rchBankAction === "invert") {
    operativaRchActiveBanks = new Set(bankNames.filter((bank) => !operativaRchActiveBanks.has(bank)));
    if (!operativaRchActiveBanks.size) {
      operativaRchActiveBanks = new Set(bankNames);
    }
  }

  renderOperativaRchBankChart();
};

const loadOperativaRchProcesses = async () => {
  const year = document.getElementById("gerenciaYear").value;
  const tbody = document.getElementById("operativaRchRows");

  try {
    const response = await fetch(`/api/reports/gerencia/rch-operativa-procesos?year=${encodeURIComponent(year)}`);
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    const data = await response.json();
    operativaRchRows = data.rows ?? [];
    operativaRchBankRows = data.bankRows ?? [];
    operativaRchBankPage = 0;
    operativaRchActiveBanks = new Set();
    operativaRchSummary = {
      year: data.year,
      totalStarted: data.totalStarted,
      totalFinished: data.totalFinished
    };
    renderOperativaRchProcesses();
  } catch (error) {
    tbody.innerHTML = `<tr><td colspan="3">No fue posible cargar Operativa RCH: ${error.message}</td></tr>`;
    document.getElementById("operativaRchChart").innerHTML = `<div class="chart-empty">Sin datos para graficar.</div>`;
    document.getElementById("operativaRchBankChart").innerHTML = `<div class="chart-empty">Sin datos para graficar.</div>`;
  }
};

const renderOperativaRchApprovedTable = () => {
  const tbody = document.getElementById("operativaRchApprovedRows");
  if (!tbody) return;

  document.getElementById("operativaRchApprovedTitle").textContent = `(RCH) Casos Aprobados por Mes ${operativaRchApprovedSummary.year}`;
  document.getElementById("operativaRchApprovedChartTitle").textContent = `(RCH) Valor Casos Aprobados ${operativaRchApprovedSummary.year}`;
  const baseRows = filterRowsByGerenciaMonth(operativaRchApprovedRows);
  const totals = getFilteredTotals(baseRows, ["cases", "amount"]);
  document.getElementById("operativaRchApprovedTotalCases").textContent = gerenciaNumberFormatter.format(hasGerenciaMonthFilter() ? totals.cases : (operativaRchApprovedSummary.totalCases ?? 0));
  document.getElementById("operativaRchApprovedTotalAmount").textContent = gerenciaNumberFormatter.format(hasGerenciaMonthFilter() ? totals.amount : (operativaRchApprovedSummary.totalAmount ?? 0));

  const rows = [...baseRows].sort((a, b) => gerenciaMonthOrder.indexOf(a.month) - gerenciaMonthOrder.indexOf(b.month));
  if (!rows.length) {
    tbody.innerHTML = `<tr><td colspan="3">Sin casos aprobados para el periodo.</td></tr>`;
    renderOperativaRchApprovedChart();
    return;
  }

  tbody.innerHTML = rows.map((row) => `
    <tr>
      <td>${escapeHtml(row.month)}</td>
      <td>${gerenciaNumberFormatter.format(row.cases ?? 0)}</td>
      <td>${gerenciaNumberFormatter.format(row.amount ?? 0)}</td>
    </tr>
  `).join("");
  renderOperativaRchApprovedChart();
};

const updateOperativaRchApprovedChartLegend = () => {
  document.querySelectorAll("[data-rch-approved-chart-series]").forEach((button) => {
    const series = button.dataset.rchApprovedChartSeries;
    button.classList.toggle("active", Boolean(operativaRchApprovedChartSeries[series]));
  });
};

const renderOperativaRchApprovedChart = () => {
  const container = document.getElementById("operativaRchApprovedChart");
  if (!container) return;

  const rows = filterRowsByGerenciaMonth([...operativaRchApprovedRows]).sort((a, b) => gerenciaMonthOrder.indexOf(a.month) - gerenciaMonthOrder.indexOf(b.month));
  updateOperativaRchApprovedChartLegend();

  if (!rows.length || !operativaRchApprovedChartSeries.amount) {
    container.innerHTML = `<div class="chart-empty">Sin datos para graficar.</div>`;
    return;
  }

  const width = 760;
  const height = 340;
  const padding = { top: 16, right: 32, bottom: 48, left: 88 };
  const chartWidth = width - padding.left - padding.right;
  const chartHeight = height - padding.top - padding.bottom;
  const maxValue = Math.max(...rows.map((row) => row.amount ?? 0), 1);
  const scaleMax = Math.max(100000000, Math.ceil(maxValue / 200000000) * 200000000);
  const ticks = [0, .25, .5, .75, 1].map((ratio) => scaleMax * ratio);
  const amountPoints = chartPointList(rows, "amount", chartWidth, chartHeight, scaleMax);

  container.innerHTML = `
    <svg viewBox="0 0 ${width} ${height}" role="img" aria-label="Valor casos aprobados RCH">
      <g transform="translate(${padding.left} ${padding.top})">
        ${ticks.map((tick) => {
          const y = chartHeight - ((tick / scaleMax) * chartHeight);
          return `
            <line class="chart-grid-line" x1="0" y1="${y}" x2="${chartWidth}" y2="${y}"></line>
            <text class="chart-y-label approved-chart-y-label" x="-10" y="${y + 4}" text-anchor="end">${gerenciaNumberFormatter.format(tick)}</text>
          `;
        }).join("")}
        <polyline class="line-meta" points="${amountPoints}"></polyline>
        ${rows.map((row, index) => {
          const point = chartPoint(rows, row, index, "amount", chartWidth, chartHeight, scaleMax);
          const x = rows.length === 1 ? 0 : (index / (rows.length - 1)) * chartWidth;
          return `
            <g class="chart-hover-group">
              <rect class="chart-hover-zone" x="${x - 18}" y="0" width="36" height="${chartHeight}" tabindex="0"></rect>
              <line class="chart-hover-guide" x1="${x}" y1="${point.y}" x2="${x}" y2="${chartHeight}"></line>
              <circle class="chart-point point-meta" cx="${point.x}" cy="${point.y}" r="4"></circle>
              <foreignObject class="chart-tooltip-box" x="${Math.min(Math.max(x + 10, 0), chartWidth - 190)}" y="0" width="190" height="100">
                <div xmlns="http://www.w3.org/1999/xhtml" class="chart-tooltip approved-chart-tooltip">
                  <strong>${escapeHtml(row.month)}</strong>
                  <span><i class="tooltip-meta"></i>Valor <b>${gerenciaNumberFormatter.format(row.amount ?? 0)}</b></span>
                  <span><i class="tooltip-total"></i>Casos <b>${gerenciaNumberFormatter.format(row.cases ?? 0)}</b></span>
                </div>
              </foreignObject>
            </g>
          `;
        }).join("")}
        ${rows.map((row, index) => {
          const x = rows.length === 1 ? 0 : (index / (rows.length - 1)) * chartWidth;
          const showLabel = index === 0 || index % 3 === 0;
          return `
            <line class="chart-x-tick" x1="${x}" y1="${chartHeight}" x2="${x}" y2="${chartHeight + 7}"></line>
            ${showLabel ? `<text class="chart-x-label" x="${x}" y="${chartHeight + 26}" text-anchor="middle">${escapeHtml(row.month)}</text>` : ""}
          `;
        }).join("")}
        <line class="chart-axis" x1="0" y1="${chartHeight}" x2="${chartWidth}" y2="${chartHeight}"></line>
      </g>
    </svg>
  `;
};

const runOperativaRchApprovedChartAction = (action) => {
  if (action === "all") {
    operativaRchApprovedChartSeries = { amount: true };
  } else if (action === "invert") {
    operativaRchApprovedChartSeries = { amount: !operativaRchApprovedChartSeries.amount };
  }

  renderOperativaRchApprovedChart();
};

const loadOperativaRchApprovedByBank = async () => {
  const year = document.getElementById("gerenciaYear").value;
  const tbody = document.getElementById("operativaRchApprovedRows");

  try {
    const response = await fetch(`/api/reports/gerencia/rch-aprobados-banco?year=${encodeURIComponent(year)}`);
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    const data = await response.json();
    operativaRchApprovedRows = data.rows ?? [];
    operativaRchApprovedSummary = {
      year: data.year,
      totalCases: data.totalCases,
      totalAmount: data.totalAmount
    };
    renderOperativaRchApprovedTable();
  } catch (error) {
    tbody.innerHTML = `<tr><td colspan="3">No fue posible cargar aprobados RCH: ${error.message}</td></tr>`;
    document.getElementById("operativaRchApprovedTotalCases").textContent = "";
    document.getElementById("operativaRchApprovedTotalAmount").textContent = "";
  }
};

const comparePnnc2025ProcessRows = (a, b) => {
  const direction = pnnc2025ProcessSort.direction === "asc" ? 1 : -1;
  let left = a[pnnc2025ProcessSort.key];
  let right = b[pnnc2025ProcessSort.key];

  if (pnnc2025ProcessSort.key === "month") {
    left = gerenciaMonthOrder.indexOf(a.month);
    right = gerenciaMonthOrder.indexOf(b.month);
  }

  left ??= -Infinity;
  right ??= -Infinity;
  if (left === right) return 0;
  return left > right ? direction : -direction;
};

const updatePnnc2025ProcessSortHeaders = () => {
  document.querySelectorAll("[data-pnnc-2025-process-sort]").forEach((button) => {
    button.classList.remove("asc", "desc");
    button.setAttribute("aria-sort", "none");

    if (button.getAttribute("data-pnnc-2025-process-sort") === pnnc2025ProcessSort.key && pnnc2025ProcessSort.direction) {
      button.classList.add(pnnc2025ProcessSort.direction);
      button.setAttribute("aria-sort", pnnc2025ProcessSort.direction === "asc" ? "ascending" : "descending");
    }
  });
};

const updatePnnc2025ProcessChartLegend = () => {
  document.querySelectorAll("[data-pnnc-2025-process-chart-series]").forEach((button) => {
    const series = button.getAttribute("data-pnnc-2025-process-chart-series");
    button.classList.toggle("active", Boolean(pnnc2025ProcessChartSeries[series]));
  });
};

const renderPnnc2025ProcessChart = () => {
  const container = document.getElementById("pnnc2025ProcessChart");
  if (!container) return;

  const rows = filterRowsByGerenciaMonth([...pnnc2025ProcessRows]).sort((a, b) => gerenciaMonthOrder.indexOf(a.month) - gerenciaMonthOrder.indexOf(b.month));
  updatePnnc2025ProcessChartLegend();

  if (!rows.length) {
    container.innerHTML = `<div class="chart-empty">Sin datos para graficar.</div>`;
    return;
  }

  const width = 860;
  const height = 320;
  const padding = { top: 14, right: 20, bottom: 46, left: 54 };
  const chartWidth = width - padding.left - padding.right;
  const chartHeight = height - padding.top - padding.bottom;
  const maxValue = Math.max(...rows.flatMap((row) => [row.started ?? 0, row.finished ?? 0]), 1);
  const scaleMax = Math.max(10, Math.ceil(maxValue / 50) * 50);
  const ticks = [0, .2, .4, .6, .8, 1].map((ratio) => scaleMax * ratio);
  const startedPoints = chartPointList(rows, "started", chartWidth, chartHeight, scaleMax);
  const finishedPoints = chartPointList(rows, "finished", chartWidth, chartHeight, scaleMax);
  const activeSeries = [
    pnnc2025ProcessChartSeries.started ? "started" : null,
    pnnc2025ProcessChartSeries.finished ? "finished" : null
  ].filter(Boolean);

  container.innerHTML = `
    <svg viewBox="0 0 ${width} ${height}" role="img" aria-label="Gráfica procesos PNNC 2025">
      <g transform="translate(${padding.left} ${padding.top})">
        ${ticks.map((tick) => {
          const y = chartHeight - ((tick / scaleMax) * chartHeight);
          return `
            <line class="chart-grid-line" x1="0" y1="${y}" x2="${chartWidth}" y2="${y}"></line>
            <text class="chart-y-label" x="-10" y="${y + 4}" text-anchor="end">${gerenciaNumberFormatter.format(tick)}</text>
          `;
        }).join("")}
        ${pnnc2025ProcessChartSeries.started ? `<polyline class="line-meta" points="${startedPoints}"></polyline>` : ""}
        ${pnnc2025ProcessChartSeries.finished ? `<polyline class="line-radicado" points="${finishedPoints}"></polyline>` : ""}
        ${rows.map((row, index) => {
          const started = chartPoint(rows, row, index, "started", chartWidth, chartHeight, scaleMax);
          const finished = chartPoint(rows, row, index, "finished", chartWidth, chartHeight, scaleMax);
          const x = rows.length === 1 ? 0 : (index / (rows.length - 1)) * chartWidth;
          const guide = activeSeries.length === 1
            ? chartPoint(rows, row, index, activeSeries[0], chartWidth, chartHeight, scaleMax)
            : null;
          return `
            <g class="chart-hover-group">
              <rect class="chart-hover-zone" x="${x - 28}" y="0" width="56" height="${chartHeight}" tabindex="0"></rect>
              ${guide ? `<line class="chart-hover-guide" x1="${x}" y1="${guide.y}" x2="${x}" y2="${chartHeight}"></line>` : ""}
              ${pnnc2025ProcessChartSeries.started ? `<circle class="chart-point point-meta" cx="${started.x}" cy="${started.y}" r="4"></circle>` : ""}
              ${pnnc2025ProcessChartSeries.finished ? `<circle class="chart-point point-radicado" cx="${finished.x}" cy="${finished.y}" r="4"></circle>` : ""}
              <foreignObject class="chart-tooltip-box" x="${Math.min(Math.max(x + 10, 0), chartWidth - 230)}" y="0" width="230" height="132">
                <div xmlns="http://www.w3.org/1999/xhtml" class="chart-tooltip">
                  <strong>${escapeHtml(row.month)}</strong>
                  <span><i class="tooltip-meta"></i>Casos Iniciados <b>${gerenciaNumberFormatter.format(row.started ?? 0)}</b></span>
                  <span><i class="tooltip-radicado"></i>Casos Finalizados <b>${gerenciaNumberFormatter.format(row.finished ?? 0)}</b></span>
                  <span><i class="tooltip-total"></i>Total <b>${gerenciaNumberFormatter.format((row.started ?? 0) + (row.finished ?? 0))}</b></span>
                </div>
              </foreignObject>
            </g>
          `;
        }).join("")}
        ${rows.map((row, index) => {
          const x = rows.length === 1 ? 0 : (index / (rows.length - 1)) * chartWidth;
          return `
            <line class="chart-x-tick" x1="${x}" y1="${chartHeight}" x2="${x}" y2="${chartHeight + 7}"></line>
            <text class="chart-x-label" x="${x}" y="${chartHeight + 26}" text-anchor="middle">${escapeHtml(row.month)}</text>
          `;
        }).join("")}
        <line class="chart-axis" x1="0" y1="${chartHeight}" x2="${chartWidth}" y2="${chartHeight}"></line>
      </g>
    </svg>
  `;
};

const renderPnnc2025Processes = () => {
  const tbody = document.getElementById("pnnc2025ProcessRows");
  if (!tbody) return;

  const baseRows = filterRowsByGerenciaMonth(pnnc2025ProcessRows);
  const rows = pnnc2025ProcessSort.direction
    ? [...baseRows].sort(comparePnnc2025ProcessRows)
    : baseRows;
  const totals = getFilteredTotals(baseRows, ["started", "finished"]);

  updatePnnc2025ProcessSortHeaders();
  document.getElementById("pnnc2025ProcessesTableTitle").textContent = "(GER) Procesos iniciados y finalizados insolvencia";
  document.getElementById("pnnc2025ProcessesChartTitle").textContent = "(GER) Gráfica procesos iniciados y finalizados insolvencia";
  document.getElementById("pnnc2025ProcessTotalStarted").textContent = gerenciaNumberFormatter.format(hasGerenciaMonthFilter() ? totals.started : (pnnc2025ProcessSummary.totalStarted ?? 0));
  document.getElementById("pnnc2025ProcessTotalFinished").textContent = gerenciaNumberFormatter.format(hasGerenciaMonthFilter() ? totals.finished : (pnnc2025ProcessSummary.totalFinished ?? 0));

  if (!rows.length) {
    tbody.innerHTML = `<tr><td colspan="3">Sin registros PNNC.</td></tr>`;
    renderPnnc2025ProcessChart();
    return;
  }

  tbody.innerHTML = rows.map((row) => `
    <tr>
      <td>${escapeHtml(row.month)}</td>
      <td>${gerenciaNumberFormatter.format(row.started ?? 0)}</td>
      <td>${gerenciaNumberFormatter.format(row.finished ?? 0)}</td>
    </tr>
  `).join("");
  renderPnnc2025ProcessChart();
};

const cyclePnnc2025ProcessSort = (key) => {
  if (pnnc2025ProcessSort.key !== key) {
    pnnc2025ProcessSort = { key, direction: key === "month" ? "asc" : "desc" };
  } else if (pnnc2025ProcessSort.direction === "desc") {
    pnnc2025ProcessSort.direction = "asc";
  } else if (pnnc2025ProcessSort.direction === "asc") {
    pnnc2025ProcessSort.direction = null;
  } else {
    pnnc2025ProcessSort.direction = key === "month" ? "asc" : "desc";
  }

  renderPnnc2025Processes();
};

const setPnnc2025ProcessChartSeries = (series) => {
  pnnc2025ProcessChartSeries = series === "started"
    ? { started: true, finished: false }
    : { started: false, finished: true };
  renderPnnc2025ProcessChart();
};

const runPnnc2025ProcessChartAction = (action) => {
  if (action === "all") {
    pnnc2025ProcessChartSeries = { started: true, finished: true };
  } else if (action === "invert") {
    pnnc2025ProcessChartSeries = {
      started: !pnnc2025ProcessChartSeries.started,
      finished: !pnnc2025ProcessChartSeries.finished
    };

    if (!pnnc2025ProcessChartSeries.started && !pnnc2025ProcessChartSeries.finished) {
      pnnc2025ProcessChartSeries = { started: true, finished: true };
    }
  }

  renderPnnc2025ProcessChart();
};

const loadPnnc2025Processes = async () => {
  const tbody = document.getElementById("pnnc2025ProcessRows");
  if (!tbody) return;

  try {
    const response = await fetch("/api/reports/gerencia/pnnc-operativa-procesos-2025?year=2025");
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    const data = await response.json();
    pnnc2025ProcessRows = data.rows ?? [];
    pnnc2025ProcessSummary = {
      year: data.year,
      totalStarted: data.totalStarted,
      totalFinished: data.totalFinished
    };
    renderPnnc2025Processes();
  } catch (error) {
    tbody.innerHTML = `<tr><td colspan="3">No fue posible cargar procesos PNNC: ${error.message}</td></tr>`;
    document.getElementById("pnnc2025ProcessChart").innerHTML = `<div class="chart-empty">Sin datos para graficar.</div>`;
  }
};

const compareOperativaPnncRows = (a, b) => {
  const direction = operativaPnncSort.direction === "asc" ? 1 : -1;
  let left = a[operativaPnncSort.key];
  let right = b[operativaPnncSort.key];

  if (operativaPnncSort.key === "stage") {
    left = String(left ?? "");
    right = String(right ?? "");
    return left.localeCompare(right, "es") * direction;
  }

  left ??= -Infinity;
  right ??= -Infinity;
  if (left === right) return 0;
  return left > right ? direction : -direction;
};

const updateOperativaPnncSortHeaders = () => {
  document.querySelectorAll("[data-pnnc-operativa-sort]").forEach((button) => {
    button.classList.remove("asc", "desc");
    button.setAttribute("aria-sort", "none");

    if (button.dataset.pnncOperativaSort === operativaPnncSort.key && operativaPnncSort.direction) {
      button.classList.add(operativaPnncSort.direction);
      button.setAttribute("aria-sort", operativaPnncSort.direction === "asc" ? "ascending" : "descending");
    }
  });
};

const renderOperativaPnncManagement = () => {
  const tbody = document.getElementById("operativaPnncRows");
  if (!tbody) return;

  const search = document.getElementById("operativaPnncSearch")?.value.trim().toLowerCase() ?? "";
  const baseRows = filterRowsByGerenciaMonth(operativaPnncRows);
  const rows = baseRows
    .filter((row) => !search || String(row.stage ?? "").toLowerCase().includes(search))
    .sort(compareOperativaPnncRows);

  updateOperativaPnncSortHeaders();
  document.getElementById("operativaPnncTotalClients").textContent = gerenciaNumberFormatter.format(operativaPnncSummary.totalClients ?? 0);
  document.getElementById("operativaPnncTotalOut").textContent = gerenciaNumberFormatter.format(operativaPnncSummary.totalOutOfManagement ?? 0);
  document.getElementById("operativaPnncTotalParticipation").textContent = operativaPnncSummary.totalParticipation == null
    ? "0.0%"
    : `${Number(operativaPnncSummary.totalParticipation).toFixed(1)}%`;

  if (!rows.length) {
    tbody.innerHTML = `<tr><td colspan="4">Sin registros Operativa PNNC.</td></tr>`;
    return;
  }

  tbody.innerHTML = rows.map((row) => `
    <tr>
      <td>${escapeHtml(row.stage)}</td>
      <td>${gerenciaNumberFormatter.format(row.clients ?? 0)}</td>
      <td class="${(row.outOfManagement ?? 0) > 0 ? "pnnc-out-cell alert" : "pnnc-out-cell"}">${gerenciaNumberFormatter.format(row.outOfManagement ?? 0)}</td>
      <td>${row.participation == null ? "0.0%" : `${Number(row.participation).toFixed(1)}%`}</td>
    </tr>
  `).join("");
};

const cycleOperativaPnncSort = (key) => {
  if (operativaPnncSort.key !== key) {
    operativaPnncSort = { key, direction: "desc" };
  } else if (operativaPnncSort.direction === "desc") {
    operativaPnncSort.direction = "asc";
  } else if (operativaPnncSort.direction === "asc") {
    operativaPnncSort.direction = null;
  } else {
    operativaPnncSort.direction = "desc";
  }

  renderOperativaPnncManagement();
};

const loadOperativaPnncManagement = async () => {
  const tbody = document.getElementById("operativaPnncRows");

  try {
    const response = await fetch("/api/reports/gerencia/pnnc-operativa-gestion");
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    const data = await response.json();
    operativaPnncRows = data.rows ?? [];
    operativaPnncSummary = {
      totalClients: data.totalClients,
      totalOutOfManagement: data.totalOutOfManagement,
      totalParticipation: data.totalParticipation
    };
    renderOperativaPnncManagement();
  } catch (error) {
    tbody.innerHTML = `<tr><td colspan="4">No fue posible cargar Operativa PNNC: ${error.message}</td></tr>`;
  }
};

const compareOperativaPnncSecondRows = (a, b) => {
  if (!operativaPnncSecondSort.direction) return 0;

  const direction = operativaPnncSecondSort.direction === "asc" ? 1 : -1;
  let left = a[operativaPnncSecondSort.key];
  let right = b[operativaPnncSecondSort.key];

  if (operativaPnncSecondSort.key === "stage") {
    left = String(left ?? "");
    right = String(right ?? "");
    return left.localeCompare(right, "es") * direction;
  }

  left ??= -Infinity;
  right ??= -Infinity;
  if (left === right) return 0;
  return left > right ? direction : -direction;
};

const updateOperativaPnncSecondSortHeaders = () => {
  document.querySelectorAll("[data-pnnc-operativa2-sort]").forEach((button) => {
    button.classList.remove("asc", "desc");
    button.setAttribute("aria-sort", "none");

    if (button.dataset.pnncOperativa2Sort === operativaPnncSecondSort.key && operativaPnncSecondSort.direction) {
      button.classList.add(operativaPnncSecondSort.direction);
      button.setAttribute("aria-sort", operativaPnncSecondSort.direction === "asc" ? "ascending" : "descending");
    }
  });
};

const renderOperativaPnncSecond = () => {
  const tbody = document.getElementById("operativaPnncSecondRows");
  if (!tbody) return;

  const rows = filterRowsByGerenciaMonth(operativaPnncSecondRows).slice().sort(compareOperativaPnncSecondRows);
  updateOperativaPnncSecondSortHeaders();

  if (!rows.length) {
    tbody.innerHTML = `<tr><td colspan="3">Sin registros Operativa PNNC.</td></tr>`;
    return;
  }

  tbody.innerHTML = rows.map((row) => {
    const outOfManagement = row.outOfManagement ?? 0;
    return `
      <tr>
        <td>${escapeHtml(row.stage)}</td>
        <td>${gerenciaNumberFormatter.format(row.negotiations ?? 0)}</td>
        <td class="pnnc-time-cell ${outOfManagement === 0 ? "zero" : ""}">${gerenciaNumberFormatter.format(outOfManagement)}</td>
      </tr>
    `;
  }).join("");
};

const cycleOperativaPnncSecondSort = (key) => {
  if (operativaPnncSecondSort.key !== key) {
    operativaPnncSecondSort = { key, direction: "desc" };
  } else if (operativaPnncSecondSort.direction === "desc") {
    operativaPnncSecondSort.direction = "asc";
  } else if (operativaPnncSecondSort.direction === "asc") {
    operativaPnncSecondSort.direction = null;
  } else {
    operativaPnncSecondSort.direction = "desc";
  }

  renderOperativaPnncSecond();
};

const loadOperativaPnncSecond = async () => {
  const tbody = document.getElementById("operativaPnncSecondRows");
  if (!tbody) return;

  try {
    const response = await fetch("/api/reports/gerencia/pnnc-operativa-insolvencia-2");
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    const data = await response.json();
    operativaPnncSecondRows = data.rows ?? [];
    renderOperativaPnncSecond();
  } catch (error) {
    tbody.innerHTML = `<tr><td colspan="3">No fue posible cargar Operativa PNNC 2: ${error.message}</td></tr>`;
  }
};

const compareOperativaPnncDetailRows = (a, b) => {
  const direction = operativaPnncDetailSort.direction === "asc" ? 1 : -1;
  let left = a[operativaPnncDetailSort.key];
  let right = b[operativaPnncDetailSort.key];

  if (["name", "stage", "responsible"].includes(operativaPnncDetailSort.key)) {
    left = String(left ?? "");
    right = String(right ?? "");
    return left.localeCompare(right, "es") * direction;
  }

  left ??= -Infinity;
  right ??= -Infinity;
  if (left === right) return 0;
  return left > right ? direction : -direction;
};

const updateOperativaPnncDetailSortHeaders = () => {
  document.querySelectorAll("[data-pnnc-operativa-detail-sort]").forEach((button) => {
    button.classList.remove("asc", "desc");
    button.setAttribute("aria-sort", "none");

    if (button.dataset.pnncOperativaDetailSort === operativaPnncDetailSort.key && operativaPnncDetailSort.direction) {
      button.classList.add(operativaPnncDetailSort.direction);
      button.setAttribute("aria-sort", operativaPnncDetailSort.direction === "asc" ? "ascending" : "descending");
    }
  });
};

const renderOperativaPnncDetail = () => {
  const tbody = document.getElementById("operativaPnncDetailRows");
  if (!tbody) return;

  const rows = filterRowsByGerenciaMonth(operativaPnncDetailRows).slice().sort(compareOperativaPnncDetailRows);
  updateOperativaPnncDetailSortHeaders();

  if (!rows.length) {
    tbody.innerHTML = `<tr><td colspan="5">Sin registros de detalle PNNC.</td></tr>`;
    return;
  }

  tbody.innerHTML = rows.map((row) => `
    <tr>
      <td>${escapeHtml(row.name)}</td>
      <td class="muted-cell">${escapeHtml(row.stage ?? "N/A")}</td>
      <td>${escapeHtml(row.responsible ?? "N/A")}</td>
      <td>${gerenciaNumberFormatter.format(row.total ?? 0)}</td>
      <td class="muted-cell">${row.daysOutOfManagement == null ? "N/A" : gerenciaNumberFormatter.format(row.daysOutOfManagement)}</td>
    </tr>
  `).join("");
};

const cycleOperativaPnncDetailSort = (key) => {
  if (operativaPnncDetailSort.key !== key) {
    operativaPnncDetailSort = { key, direction: "asc" };
  } else if (operativaPnncDetailSort.direction === "asc") {
    operativaPnncDetailSort.direction = "desc";
  } else if (operativaPnncDetailSort.direction === "desc") {
    operativaPnncDetailSort.direction = null;
  } else {
    operativaPnncDetailSort.direction = "asc";
  }

  renderOperativaPnncDetail();
};

const loadOperativaPnncDetail = async () => {
  const tbody = document.getElementById("operativaPnncDetailRows");
  if (!tbody) return;

  try {
    const response = await fetch("/api/reports/gerencia/pnnc-operativa-detalle");
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    const data = await response.json();
    operativaPnncDetailRows = data.rows ?? [];
    renderOperativaPnncDetail();
  } catch (error) {
    tbody.innerHTML = `<tr><td colspan="5">No fue posible cargar detalle PNNC: ${error.message}</td></tr>`;
  }
};

const compareRowsBySort = (sort, textKeys = []) => (a, b) => {
  if (!sort.direction) return 0;

  const direction = sort.direction === "asc" ? 1 : -1;
  let left = a[sort.key];
  let right = b[sort.key];

  if (textKeys.includes(sort.key)) {
    left = String(left ?? "");
    right = String(right ?? "");
    return left.localeCompare(right, "es") * direction;
  }

  left ??= -Infinity;
  right ??= -Infinity;
  if (left === right) return 0;
  return left > right ? direction : -direction;
};

const updateSortHeaders = (selector, sort) => {
  document.querySelectorAll(selector).forEach((button) => {
    button.classList.remove("asc", "desc");
    button.setAttribute("aria-sort", "none");

    const key = button.dataset.lpMonthlySort
      ?? button.dataset.lpWeeklySort
      ?? button.dataset.lpEmbargosSort
      ?? button.dataset.lpLibranzaSort
      ?? button.dataset.insEmbargosSort
      ?? button.dataset.insLibranzaSort
      ?? button.dataset.insuranceKpiSort
      ?? button.dataset.insuranceCommercialSort
      ?? button.dataset.insuranceCallsSort
      ?? button.dataset.insuranceQuotesSort
      ?? button.dataset.insuranceOutSort
      ?? button.dataset.insuranceOutDetailSort;
    if (key === sort.key && sort.direction) {
      button.classList.add(sort.direction);
      button.setAttribute("aria-sort", sort.direction === "asc" ? "ascending" : "descending");
    }
  });
};

const cycleSort = (sort, key, defaultDirection = "desc") => {
  if (sort.key !== key) {
    return { key, direction: defaultDirection };
  }

  if (sort.direction === defaultDirection) {
    return { key, direction: defaultDirection === "asc" ? "desc" : "asc" };
  }

  if (sort.direction) {
    return { key, direction: null };
  }

  return { key, direction: defaultDirection };
};

const loadPnncLpCompliance2025 = async () => {
  const target = document.getElementById("pnncLpCompliance2025");
  if (!target) return;

  try {
    const response = await fetch("/api/reports/gerencia/pnnc-lp-compliance-2025");
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    const data = await response.json();
    target.textContent = data.compliance == null ? "0.0%" : gerenciaPercentFormatter.format(data.compliance);
  } catch (error) {
    target.textContent = "0.0%";
  }
};

const renderLpMonthlyTasks = () => {
  const tbody = document.getElementById("lpMonthlyTaskRows");
  if (!tbody) return;

  const baseRows = filterRowsByGerenciaMonth(lpMonthlyTaskRows);
  const rows = baseRows.slice().sort(compareRowsBySort(lpMonthlyTaskSort, ["month"]));
  const totals = getFilteredTotals(baseRows, ["totalClients", "totalTasks", "completed", "pending", "lateOpen", "lateClosed"]);
  const totalPercentage = totals.totalTasks ? totals.lateClosed / totals.totalTasks : 0;
  updateSortHeaders("[data-lp-monthly-sort]", lpMonthlyTaskSort);

  document.getElementById("lpMonthlyTotalClients").textContent = gerenciaNumberFormatter.format(hasGerenciaMonthFilter() ? totals.totalClients : (lpMonthlyTaskSummary.totalClients ?? 0));
  document.getElementById("lpMonthlyTotalTasks").textContent = gerenciaNumberFormatter.format(hasGerenciaMonthFilter() ? totals.totalTasks : (lpMonthlyTaskSummary.totalTasks ?? 0));
  document.getElementById("lpMonthlyTotalCompleted").textContent = gerenciaNumberFormatter.format(hasGerenciaMonthFilter() ? totals.completed : (lpMonthlyTaskSummary.totalCompleted ?? 0));
  document.getElementById("lpMonthlyTotalPending").textContent = gerenciaNumberFormatter.format(hasGerenciaMonthFilter() ? totals.pending : (lpMonthlyTaskSummary.totalPending ?? 0));
  document.getElementById("lpMonthlyTotalLateOpen").textContent = gerenciaNumberFormatter.format(hasGerenciaMonthFilter() ? totals.lateOpen : (lpMonthlyTaskSummary.totalLateOpen ?? 0));
  document.getElementById("lpMonthlyTotalLateClosed").textContent = gerenciaNumberFormatter.format(hasGerenciaMonthFilter() ? totals.lateClosed : (lpMonthlyTaskSummary.totalLateClosed ?? 0));
  document.getElementById("lpMonthlyTotalPercentage").textContent = gerenciaPercentFormatter.format(hasGerenciaMonthFilter() ? totalPercentage : (lpMonthlyTaskSummary.totalPercentage ?? 0));

  if (!rows.length) {
    tbody.innerHTML = `<tr><td colspan="8">Sin registros LP mensuales.</td></tr>`;
    return;
  }

  tbody.innerHTML = rows.map((row) => `
    <tr>
      <td>${escapeHtml(row.month)}</td>
      <td>${gerenciaNumberFormatter.format(row.totalClients ?? 0)}</td>
      <td>${gerenciaNumberFormatter.format(row.totalTasks ?? 0)}</td>
      <td>${gerenciaNumberFormatter.format(row.completed ?? 0)}</td>
      <td>${gerenciaNumberFormatter.format(row.pending ?? 0)}</td>
      <td class="lp-ok-cell">${gerenciaNumberFormatter.format(row.lateOpen ?? 0)}</td>
      <td class="${(row.lateClosed ?? 0) > 0 ? "lp-alert-cell" : "lp-ok-cell"}">${gerenciaNumberFormatter.format(row.lateClosed ?? 0)}</td>
      <td>${gerenciaPercentFormatter.format(row.percentage ?? 0)}</td>
    </tr>
  `).join("");
};

const loadLpMonthlyTasks = async () => {
  const tbody = document.getElementById("lpMonthlyTaskRows");
  if (!tbody) return;

  try {
    const response = await fetch("/api/reports/gerencia/lp-monthly-tasks?year=2025");
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    const data = await response.json();
    lpMonthlyTaskRows = data.rows ?? [];
    lpMonthlyTaskSummary = data;
    renderLpMonthlyTasks();
  } catch (error) {
    tbody.innerHTML = `<tr><td colspan="8">No fue posible cargar gestiones mensuales LP: ${error.message}</td></tr>`;
  }
};

const renderLpWeeklyTasks = () => {
  const tbody = document.getElementById("lpWeeklyTaskRows");
  if (!tbody) return;

  const baseRows = filterRowsByGerenciaMonth(lpWeeklyTaskRows);
  const rows = baseRows.slice().sort(compareRowsBySort(lpWeeklyTaskSort, ["week"]));
  const totals = getFilteredTotals(baseRows, ["totalTasks", "completed", "pending", "lateOpen", "lateClosed"]);
  const totalPercentage = totals.totalTasks ? totals.lateClosed / totals.totalTasks : 0;
  updateSortHeaders("[data-lp-weekly-sort]", lpWeeklyTaskSort);

  document.getElementById("lpWeeklyTotalTasks").textContent = gerenciaNumberFormatter.format(hasGerenciaMonthFilter() ? totals.totalTasks : (lpWeeklyTaskSummary.totalTasks ?? 0));
  document.getElementById("lpWeeklyTotalCompleted").textContent = gerenciaNumberFormatter.format(hasGerenciaMonthFilter() ? totals.completed : (lpWeeklyTaskSummary.totalCompleted ?? 0));
  document.getElementById("lpWeeklyTotalPending").textContent = gerenciaNumberFormatter.format(hasGerenciaMonthFilter() ? totals.pending : (lpWeeklyTaskSummary.totalPending ?? 0));
  document.getElementById("lpWeeklyTotalLateOpen").textContent = gerenciaNumberFormatter.format(hasGerenciaMonthFilter() ? totals.lateOpen : (lpWeeklyTaskSummary.totalLateOpen ?? 0));
  document.getElementById("lpWeeklyTotalLateClosed").textContent = gerenciaNumberFormatter.format(hasGerenciaMonthFilter() ? totals.lateClosed : (lpWeeklyTaskSummary.totalLateClosed ?? 0));
  document.getElementById("lpWeeklyTotalPercentage").textContent = gerenciaPercentFormatter.format(hasGerenciaMonthFilter() ? totalPercentage : (lpWeeklyTaskSummary.totalPercentage ?? 0));

  if (!rows.length) {
    tbody.innerHTML = `<tr><td colspan="7">Sin registros LP semanales.</td></tr>`;
    return;
  }

  tbody.innerHTML = rows.map((row) => `
    <tr>
      <td>${escapeHtml(row.week)}</td>
      <td>${gerenciaNumberFormatter.format(row.totalTasks ?? 0)}</td>
      <td>${gerenciaNumberFormatter.format(row.completed ?? 0)}</td>
      <td>${gerenciaNumberFormatter.format(row.pending ?? 0)}</td>
      <td class="lp-ok-cell">${gerenciaNumberFormatter.format(row.lateOpen ?? 0)}</td>
      <td class="${(row.lateClosed ?? 0) > 0 ? "lp-alert-cell" : "lp-ok-cell"}">${gerenciaNumberFormatter.format(row.lateClosed ?? 0)}</td>
      <td>${gerenciaPercentFormatter.format(row.percentage ?? 0)}</td>
    </tr>
  `).join("");
};

const loadLpWeeklyTasks = async () => {
  const tbody = document.getElementById("lpWeeklyTaskRows");
  if (!tbody) return;

  try {
    const response = await fetch("/api/reports/gerencia/lp-weekly-tasks?year=2025");
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    const data = await response.json();
    lpWeeklyTaskRows = data.rows ?? [];
    lpWeeklyTaskSummary = data;
    renderLpWeeklyTasks();
  } catch (error) {
    tbody.innerHTML = `<tr><td colspan="7">No fue posible cargar control semanal LP: ${error.message}</td></tr>`;
  }
};

const renderLpSpecialTasks = (config) => {
  const tbody = document.getElementById(config.rowsId);
  if (!tbody) return;

  const baseRows = filterRowsByGerenciaMonth(config.rows);
  const rows = baseRows.slice().sort(compareRowsBySort(config.sort, ["month", "responsible", "pipeline"]));
  const totals = getFilteredTotals(baseRows, ["totalClients", "totalTasks", "completed", "pending", "lateOpen", "lateClosed"]);
  const totalPercentage = totals.totalTasks ? totals.lateClosed / totals.totalTasks : 0;
  updateSortHeaders(config.sortSelector, config.sort);

  document.getElementById(config.totalIds.clients).textContent = gerenciaNumberFormatter.format(hasGerenciaMonthFilter() ? totals.totalClients : (config.summary.totalClients ?? 0));
  document.getElementById(config.totalIds.tasks).textContent = gerenciaNumberFormatter.format(hasGerenciaMonthFilter() ? totals.totalTasks : (config.summary.totalTasks ?? 0));
  document.getElementById(config.totalIds.completed).textContent = gerenciaNumberFormatter.format(hasGerenciaMonthFilter() ? totals.completed : (config.summary.totalCompleted ?? 0));
  document.getElementById(config.totalIds.pending).textContent = gerenciaNumberFormatter.format(hasGerenciaMonthFilter() ? totals.pending : (config.summary.totalPending ?? 0));
  document.getElementById(config.totalIds.lateOpen).textContent = gerenciaNumberFormatter.format(hasGerenciaMonthFilter() ? totals.lateOpen : (config.summary.totalLateOpen ?? 0));
  document.getElementById(config.totalIds.lateClosed).textContent = gerenciaNumberFormatter.format(hasGerenciaMonthFilter() ? totals.lateClosed : (config.summary.totalLateClosed ?? 0));
  document.getElementById(config.totalIds.percentage).textContent = gerenciaPercentFormatter.format(hasGerenciaMonthFilter() ? totalPercentage : (config.summary.totalPercentage ?? 0));

  if (!rows.length) {
    tbody.innerHTML = `<tr><td colspan="8">Sin registros.</td></tr>`;
    return;
  }

  tbody.innerHTML = rows.map((row) => `
    <tr>
      <td>${escapeHtml(row.month)}</td>
      <td>${gerenciaNumberFormatter.format(row.totalClients ?? 0)}</td>
      <td>${gerenciaNumberFormatter.format(row.totalTasks ?? 0)}</td>
      <td>${gerenciaNumberFormatter.format(row.completed ?? 0)}</td>
      <td>${gerenciaNumberFormatter.format(row.pending ?? 0)}</td>
      <td class="${(row.lateOpen ?? 0) > 0 ? "lp-alert-cell" : "lp-ok-cell"}">${gerenciaNumberFormatter.format(row.lateOpen ?? 0)}</td>
      <td class="${(row.lateClosed ?? 0) > 0 ? "lp-alert-cell" : "lp-ok-cell"}">${gerenciaNumberFormatter.format(row.lateClosed ?? 0)}</td>
      <td>${gerenciaPercentFormatter.format(row.percentage ?? 0)}</td>
    </tr>
  `).join("");
};

const renderLpEmbargosTasks = () => renderLpSpecialTasks({
  rows: lpEmbargosTaskRows,
  summary: lpEmbargosTaskSummary,
  sort: lpEmbargosTaskSort,
  rowsId: "lpEmbargosTaskRows",
  sortSelector: "[data-lp-embargos-sort]",
  totalIds: {
    clients: "lpEmbargosTotalClients",
    tasks: "lpEmbargosTotalTasks",
    completed: "lpEmbargosTotalCompleted",
    pending: "lpEmbargosTotalPending",
    lateOpen: "lpEmbargosTotalLateOpen",
    lateClosed: "lpEmbargosTotalLateClosed",
    percentage: "lpEmbargosTotalPercentage"
  }
});

const renderLpLibranzaTasks = () => renderLpSpecialTasks({
  rows: lpLibranzaTaskRows,
  summary: lpLibranzaTaskSummary,
  sort: lpLibranzaTaskSort,
  rowsId: "lpLibranzaTaskRows",
  sortSelector: "[data-lp-libranza-sort]",
  totalIds: {
    clients: "lpLibranzaTotalClients",
    tasks: "lpLibranzaTotalTasks",
    completed: "lpLibranzaTotalCompleted",
    pending: "lpLibranzaTotalPending",
    lateOpen: "lpLibranzaTotalLateOpen",
    lateClosed: "lpLibranzaTotalLateClosed",
    percentage: "lpLibranzaTotalPercentage"
  }
});

const loadLpEmbargosTasks = async () => {
  const tbody = document.getElementById("lpEmbargosTaskRows");
  if (!tbody) return;

  try {
    const response = await fetch("/api/reports/gerencia/lp-embargos-tasks?year=2025");
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    const data = await response.json();
    lpEmbargosTaskRows = data.rows ?? [];
    lpEmbargosTaskSummary = data;
    renderLpEmbargosTasks();
  } catch (error) {
    tbody.innerHTML = `<tr><td colspan="8">No fue posible cargar gestiones Embargos: ${error.message}</td></tr>`;
  }
};

const loadLpLibranzaTasks = async () => {
  const tbody = document.getElementById("lpLibranzaTaskRows");
  if (!tbody) return;

  try {
    const response = await fetch("/api/reports/gerencia/lp-libranza-tasks?year=2025");
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    const data = await response.json();
    lpLibranzaTaskRows = data.rows ?? [];
    lpLibranzaTaskSummary = data;
    renderLpLibranzaTasks();
  } catch (error) {
    tbody.innerHTML = `<tr><td colspan="8">No fue posible cargar gestiones Libranza: ${error.message}</td></tr>`;
  }
};

const renderInsDetailRows = (rows, sort, rowsId, sortSelector) => {
  const tbody = document.getElementById(rowsId);
  if (!tbody) return;

  const sortedRows = rows.slice().sort(compareRowsBySort(sort, ["name", "stage", "responsible"]));
  updateSortHeaders(sortSelector, sort);

  if (!sortedRows.length) {
    tbody.innerHTML = `<tr><td colspan="5">Sin registros.</td></tr>`;
    return;
  }

  tbody.innerHTML = sortedRows.map((row) => `
    <tr>
      <td>${escapeHtml(row.name)}</td>
      <td>${escapeHtml(row.stage ?? "N/A")}</td>
      <td>${escapeHtml(row.responsible ?? "N/A")}</td>
      <td>${gerenciaNumberFormatter.format(row.total ?? 0)}</td>
      <td class="muted-cell">${row.daysOutOfManagement == null ? "N/A" : gerenciaNumberFormatter.format(row.daysOutOfManagement)}</td>
    </tr>
  `).join("");
};

const renderInsEmbargosDetail = () => renderInsDetailRows(insEmbargosRows, insEmbargosSort, "insEmbargosRows", "[data-ins-embargos-sort]");
const renderInsLibranzaDetail = () => renderInsDetailRows(insLibranzaRows, insLibranzaSort, "insLibranzaRows", "[data-ins-libranza-sort]");

const loadInsEmbargosDetail = async () => {
  const tbody = document.getElementById("insEmbargosRows");
  if (!tbody) return;

  try {
    const response = await fetch("/api/reports/gerencia/ins-embargos-detail");
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    const data = await response.json();
    insEmbargosRows = data.rows ?? [];
    renderInsEmbargosDetail();
  } catch (error) {
    tbody.innerHTML = `<tr><td colspan="5">No fue posible cargar reporte Embargos: ${error.message}</td></tr>`;
  }
};

const loadInsLibranzaDetail = async () => {
  const tbody = document.getElementById("insLibranzaRows");
  if (!tbody) return;

  try {
    const response = await fetch("/api/reports/gerencia/ins-libranza-detail");
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    const data = await response.json();
    insLibranzaRows = data.rows ?? [];
    renderInsLibranzaDetail();
  } catch (error) {
    tbody.innerHTML = `<tr><td colspan="5">No fue posible cargar reporte Libranza: ${error.message}</td></tr>`;
  }
};

const formatInsurancePercent = (value) => value == null ? "N/A" : gerenciaPercentFormatter.format(value);

const renderInsuranceCompliance = () => {
  const kpiTarget = document.getElementById("insuranceComplianceValue");
  const kpiBody = document.getElementById("insuranceKpiRows");
  const commercialBody = document.getElementById("insuranceCommercialRows");
  if (!kpiTarget || !kpiBody || !commercialBody) return;

  const filteredKpiRows = filterRowsByGerenciaMonth(insuranceKpiRows);
  const filteredCommercialRows = filterRowsByGerenciaMonth(insuranceCommercialRows);
  const hasMonthFilter = hasGerenciaMonthFilter();
  const selectedCompliance = filteredKpiRows.length
    ? filteredKpiRows.reduce((sum, row) => sum + (row.totalCompliance ?? 0), 0) / filteredKpiRows.length
    : 0;
  kpiTarget.textContent = gerenciaPercentFormatter.format(hasMonthFilter ? selectedCompliance : (insuranceCompliance ?? 0));
  updateSortHeaders("[data-insurance-kpi-sort]", insuranceKpiSort);
  updateSortHeaders("[data-insurance-commercial-sort]", insuranceCommercialSort);

  const kpiRows = filteredKpiRows.slice().sort(compareRowsBySort(insuranceKpiSort, ["month"]));
  const commercialRows = filteredCommercialRows.slice().sort(compareRowsBySort(insuranceCommercialSort, ["month"]));

  if (!kpiRows.length) {
    kpiBody.innerHTML = `<tr><td colspan="4">Sin registros de cumplimiento seguros.</td></tr>`;
  } else {
    kpiBody.innerHTML = kpiRows.map((row) => `
      <tr>
        <td>${escapeHtml(row.month)}</td>
        <td>${formatInsurancePercent(row.callCompliance)}</td>
        <td>${formatInsurancePercent(row.salesCompliance)}</td>
        <td class="compliance-cell ${complianceTone(row.totalCompliance)}">${formatInsurancePercent(row.totalCompliance)}</td>
      </tr>
    `).join("");
  }

  if (!commercialRows.length) {
    commercialBody.innerHTML = `<tr><td colspan="4">Sin registros comerciales seguros.</td></tr>`;
  } else {
    commercialBody.innerHTML = commercialRows.map((row) => `
      <tr>
        <td>${escapeHtml(row.month)}</td>
        <td>${gerenciaNumberFormatter.format(row.monthlyTarget ?? 0)}</td>
        <td>${gerenciaNumberFormatter.format(row.totalSales ?? 0)}</td>
        <td class="compliance-cell ${complianceTone(row.compliance)}">${formatInsurancePercent(row.compliance)}</td>
      </tr>
    `).join("");
  }
};

const loadInsuranceCompliance = async () => {
  const kpiBody = document.getElementById("insuranceKpiRows");
  const commercialBody = document.getElementById("insuranceCommercialRows");
  if (!kpiBody || !commercialBody) return;

  try {
    const response = await fetch("/api/reports/gerencia/seguros-cumplimiento");
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    const data = await response.json();
    insuranceCompliance = data.compliance ?? 0;
    insuranceKpiRows = data.kpiRows ?? [];
    insuranceCommercialRows = data.commercialRows ?? [];
    renderInsuranceCompliance();
  } catch (error) {
    document.getElementById("insuranceComplianceValue").textContent = "0.0%";
    kpiBody.innerHTML = `<tr><td colspan="4">No fue posible cargar KPI seguros: ${error.message}</td></tr>`;
    commercialBody.innerHTML = `<tr><td colspan="4">No fue posible cargar comercial seguros: ${error.message}</td></tr>`;
  }
};

const renderInsuranceOperations = () => {
  const callsBody = document.getElementById("insuranceCallsRows");
  const quotesBody = document.getElementById("insuranceQuotesRows");
  if (!callsBody || !quotesBody) return;

  updateSortHeaders("[data-insurance-calls-sort]", insuranceCallsSort);
  updateSortHeaders("[data-insurance-quotes-sort]", insuranceQuotesSort);

  const callRows = filterRowsByGerenciaMonth(insuranceCallsRows).slice().sort(compareRowsBySort(insuranceCallsSort, ["monthNumber"]));
  const quoteRows = filterRowsByGerenciaMonth(insuranceQuotesRows).slice().sort(compareRowsBySort(insuranceQuotesSort, ["monthNumber"]));

  if (!callRows.length) {
    callsBody.innerHTML = `<tr><td colspan="6">Sin registros de llamadas seguros.</td></tr>`;
  } else {
    callsBody.innerHTML = callRows.map((row) => `
      <tr>
        <td>${escapeHtml(row.month)}</td>
        <td>${gerenciaNumberFormatter.format(row.monthlyTarget ?? 0)}</td>
        <td>${gerenciaNumberFormatter.format(row.outgoingCalls ?? 0)}</td>
        <td>${gerenciaNumberFormatter.format(row.effectiveCalls ?? 0)}</td>
        <td>${gerenciaNumberFormatter.format(row.rejectedCalls ?? 0)}</td>
        <td class="compliance-cell ${complianceTone(row.compliance)}">${formatInsurancePercent(row.compliance)}</td>
      </tr>
    `).join("");
  }

  if (!quoteRows.length) {
    quotesBody.innerHTML = `<tr><td colspan="4">Sin registros de cotizaciones seguros.</td></tr>`;
  } else {
    quotesBody.innerHTML = quoteRows.map((row) => `
      <tr>
        <td>${escapeHtml(row.month)}</td>
        <td>${gerenciaNumberFormatter.format(row.monthlyTarget ?? 0)}</td>
        <td>${gerenciaNumberFormatter.format(row.generatedQuotes ?? 0)}</td>
        <td class="compliance-cell ${complianceTone(row.compliance)}">${formatInsurancePercent(row.compliance)}</td>
      </tr>
    `).join("");
  }
};

const loadInsuranceOperations = async () => {
  const callsBody = document.getElementById("insuranceCallsRows");
  const quotesBody = document.getElementById("insuranceQuotesRows");
  if (!callsBody || !quotesBody) return;

  try {
    const response = await fetch("/api/reports/gerencia/seguros-operaciones-mensuales");
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    const data = await response.json();
    insuranceCallsRows = data.callRows ?? [];
    insuranceQuotesRows = data.quoteRows ?? [];
    renderInsuranceOperations();
  } catch (error) {
    callsBody.innerHTML = `<tr><td colspan="6">No fue posible cargar llamadas seguros: ${error.message}</td></tr>`;
    quotesBody.innerHTML = `<tr><td colspan="4">No fue posible cargar cotizaciones seguros: ${error.message}</td></tr>`;
  }
};

const insuranceOutDetailMatchesSearch = (row) => {
  const term = insuranceOutDetailSearch.trim().toLowerCase();
  if (!term) return true;

  return [
    row.id,
    row.name,
    row.stage,
    row.daysOutOfManagement
  ].some((value) => String(value ?? "").toLowerCase().includes(term));
};

const renderInsuranceOutOfTime = () => {
  const summaryBody = document.getElementById("insuranceOutRows");
  const detailBody = document.getElementById("insuranceOutDetailRows");
  if (!summaryBody || !detailBody) return;

  updateSortHeaders("[data-insurance-out-sort]", insuranceOutSort);
  updateSortHeaders("[data-insurance-out-detail-sort]", insuranceOutDetailSort);

  const summaryRows = filterRowsByGerenciaMonth(insuranceOutRows).slice().sort(compareRowsBySort(insuranceOutSort, ["stage"]));
  const detailRows = filterRowsByGerenciaMonth(insuranceOutDetailRows)
    .filter(insuranceOutDetailMatchesSearch)
    .sort(compareRowsBySort(insuranceOutDetailSort, ["stage", "name"]));
  const outTotals = getFilteredTotals(summaryRows, ["totalNegotiations", "outOfTime"]);

  document.getElementById("insuranceOutTotalNegotiations").textContent = gerenciaNumberFormatter.format(hasGerenciaMonthFilter() ? outTotals.totalNegotiations : (insuranceOutTotals.totalNegotiations ?? 0));
  document.getElementById("insuranceOutTotalLate").textContent = gerenciaNumberFormatter.format(hasGerenciaMonthFilter() ? outTotals.outOfTime : (insuranceOutTotals.outOfTime ?? 0));

  if (!summaryRows.length) {
    summaryBody.innerHTML = `<tr><td colspan="3">Sin registros de negociaciones fuera de tiempo.</td></tr>`;
  } else {
    summaryBody.innerHTML = summaryRows.map((row) => `
      <tr>
        <td>${escapeHtml(row.stage)}</td>
        <td>${gerenciaNumberFormatter.format(row.totalNegotiations ?? 0)}</td>
        <td class="${(row.outOfTime ?? 0) > 0 ? "out-alert" : ""}">${gerenciaNumberFormatter.format(row.outOfTime ?? 0)}</td>
      </tr>
    `).join("");
  }

  if (!detailRows.length) {
    detailBody.innerHTML = `<tr><td colspan="4">Sin registros para la busqueda actual.</td></tr>`;
  } else {
    detailBody.innerHTML = detailRows.map((row) => `
      <tr>
        <td>${escapeHtml(row.id)}</td>
        <td>${escapeHtml(row.name)}</td>
        <td>${escapeHtml(row.stage)}</td>
        <td>${gerenciaNumberFormatter.format(row.daysOutOfManagement ?? 0)}</td>
      </tr>
    `).join("");
  }
};

const loadInsuranceOutOfTime = async () => {
  const summaryBody = document.getElementById("insuranceOutRows");
  const detailBody = document.getElementById("insuranceOutDetailRows");
  if (!summaryBody || !detailBody) return;

  try {
    const response = await fetch("/api/reports/gerencia/seguros-fuera-tiempo");
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    const data = await response.json();
    insuranceOutRows = data.summaryRows ?? [];
    insuranceOutDetailRows = data.detailRows ?? [];
    insuranceOutTotals = data.totals ?? {};
    renderInsuranceOutOfTime();
  } catch (error) {
    summaryBody.innerHTML = `<tr><td colspan="3">No fue posible cargar negociaciones fuera de tiempo: ${error.message}</td></tr>`;
    detailBody.innerHTML = `<tr><td colspan="4">No fue posible cargar detalle fuera de tiempo: ${error.message}</td></tr>`;
  }
};

const customerServiceColors = ["#27abc4", "#444f82", "#5bbf82", "#ff7a45", "#666666", "#e23c61", "#7c5fc9"];

const getCustomerServiceRequirementTotals = () => {
  const totals = new Map();
  filterRowsByGerenciaMonth(customerServiceRequirements).forEach((row) => {
    const requirement = row.requirement || "SIN DEFINIR";
    totals.set(requirement, (totals.get(requirement) ?? 0) + Number(row.cases ?? 0));
  });

  return [...totals.entries()]
    .map(([requirement, cases]) => ({ requirement, cases }))
    .sort((a, b) => b.cases - a.cases);
};

const syncCustomerServiceActiveRequirements = () => {
  const names = getCustomerServiceRequirementTotals().map((item) => item.requirement);
  const valid = new Set(names);
  customerServiceActiveRequirements = new Set([...customerServiceActiveRequirements].filter((name) => valid.has(name)));

  if (!customerServiceActiveRequirements.size && names.length) {
    customerServiceActiveRequirements = new Set(names);
  }
};

const renderCustomerServiceRequirementLegend = () => {
  const legend = document.getElementById("customerServiceRequirementLegend");
  if (!legend) return;
  const requirements = getCustomerServiceRequirementTotals();

  legend.innerHTML = `
    <div class="bank-legend-series">
      ${requirements.map((item, index) => `
        <button class="bank-toggle ${customerServiceActiveRequirements.has(item.requirement) ? "active" : ""}" type="button" data-customer-service-requirement="${escapeHtml(item.requirement)}">
          <i style="--bank-color: ${customerServiceColors[index % customerServiceColors.length]}"></i>${escapeHtml(item.requirement)}
        </button>
      `).join("")}
    </div>
    <div class="bank-legend-actions">
      <button class="legend-chip" type="button" data-customer-service-requirement-action="all">All</button>
      <button class="legend-chip" type="button" data-customer-service-requirement-action="invert">Inv</button>
    </div>
  `;
};

const attachCustomerServiceRequirementTooltip = (container, items, total) => {
  const tooltip = container.querySelector(".bank-donut-html-tooltip");
  if (!tooltip) return;

  const showTooltip = (item) => {
    const percent = total ? item.cases / total : 0;
    tooltip.innerHTML = `
      <strong>${escapeHtml(item.requirement)}</strong>
      <span>SUM(# de Casos)</span>
      <b>${gerenciaNumberFormatter.format(item.cases)} &nbsp; ${gerenciaPercentFormatter.format(percent)}</b>
    `;
    tooltip.classList.add("visible");
    tooltip.setAttribute("aria-hidden", "false");
  };

  const hideTooltip = () => {
    tooltip.classList.remove("visible");
    tooltip.setAttribute("aria-hidden", "true");
  };

  container.querySelectorAll("[data-customer-service-segment]").forEach((segment) => {
    const item = items[Number(segment.dataset.customerServiceSegment)];
    if (!item) return;

    segment.addEventListener("mouseenter", () => showTooltip(item));
    segment.addEventListener("focus", () => showTooltip(item));
    segment.addEventListener("mouseleave", hideTooltip);
    segment.addEventListener("blur", hideTooltip);
  });
};

const renderCustomerServiceRequirementChart = () => {
  const container = document.getElementById("customerServiceRequirementChart");
  if (!container) return;

  syncCustomerServiceActiveRequirements();
  renderCustomerServiceRequirementLegend();

  const items = getCustomerServiceRequirementTotals()
    .map((item, index) => ({
      ...item,
      color: customerServiceColors[index % customerServiceColors.length]
    }))
    .filter((item) => customerServiceActiveRequirements.has(item.requirement) && item.cases > 0);
  const total = items.reduce((sum, item) => sum + (item.cases ?? 0), 0);

  if (!items.length || !total) {
    container.innerHTML = `<div class="chart-empty">Sin datos para graficar.</div>`;
    return;
  }

  const width = 420;
  const height = 300;
  const centerX = 210;
  const centerY = 158;
  const outerRadius = 112;
  const innerRadius = 48;
  let currentAngle = 0;

  container.innerHTML = `
    <svg viewBox="0 0 ${width} ${height}" role="img" aria-label="Total de requerimientos">
      ${items.map((item, index) => {
        const angle = (item.cases / total) * 360;
        const startAngle = currentAngle;
        const endAngle = currentAngle + Math.min(angle, 359.99);
        const midAngle = startAngle + (angle / 2);
        currentAngle += angle;
        const labelPoint = bankDonutPoint(centerX, centerY, outerRadius + 28, midAngle);
        const percent = item.cases / total;

        return `
          <g class="bank-donut-segment-group">
            <path class="bank-donut-segment" d="${bankDonutPath(centerX, centerY, outerRadius, innerRadius, startAngle, endAngle)}" fill="${item.color}" tabindex="0" data-customer-service-segment="${index}"></path>
            ${percent >= 0.035 ? `<text class="customer-service-donut-label" x="${labelPoint.x.toFixed(1)}" y="${labelPoint.y.toFixed(1)}" text-anchor="${labelPoint.x >= centerX ? "start" : "end"}">${escapeHtml(item.requirement)}</text>` : ""}
          </g>
        `;
      }).join("")}
    </svg>
    <div class="bank-donut-html-tooltip" aria-hidden="true"></div>
  `;

  attachCustomerServiceRequirementTooltip(container, items, total);
};

const renderCustomerServiceMonthlyLegend = () => {
  document.querySelectorAll("[data-customer-service-monthly-series]").forEach((button) => {
    button.classList.toggle("active", Boolean(customerServiceMonthlySeries[button.dataset.customerServiceMonthlySeries]));
  });
};

const renderCustomerServiceMonthlyChart = () => {
  const container = document.getElementById("customerServiceMonthlyChart");
  if (!container) return;

  renderCustomerServiceMonthlyLegend();

  const rows = filterRowsByGerenciaMonth(customerServiceMonthlyRows).sort((a, b) => (a.monthNumber ?? 0) - (b.monthNumber ?? 0));
  if (!rows.length || !customerServiceMonthlySeries.received) {
    container.innerHTML = `<div class="chart-empty">Sin datos para graficar.</div>`;
    return;
  }

  const width = 430;
  const height = 300;
  const padding = { top: 18, right: 24, bottom: 40, left: 56 };
  const chartWidth = width - padding.left - padding.right;
  const chartHeight = height - padding.top - padding.bottom;
  const maxValue = Math.max(...rows.map((row) => row.received ?? 0), 1);
  const scaleMax = Math.max(1000, Math.ceil(maxValue / 200) * 200);
  const ticks = [0, .2, .4, .6, .8, 1].map((ratio) => scaleMax * ratio);
  const barGap = 10;
  const barWidth = Math.max(18, (chartWidth - (barGap * (rows.length - 1))) / rows.length);

  container.innerHTML = `
    <svg viewBox="0 0 ${width} ${height}" role="img" aria-label="PQRFs por mes">
      <g transform="translate(${padding.left} ${padding.top})">
        ${ticks.map((tick) => {
          const y = chartHeight - ((tick / scaleMax) * chartHeight);
          return `
            <line class="chart-grid-line" x1="0" y1="${y}" x2="${chartWidth}" y2="${y}"></line>
            <text class="chart-y-label" x="-10" y="${y + 4}" text-anchor="end">${tick >= 1000 ? "1k" : gerenciaNumberFormatter.format(tick)}</text>
          `;
        }).join("")}
        ${rows.map((row, index) => {
          const x = index * (barWidth + barGap);
          const value = row.received ?? 0;
          const barHeight = (value / scaleMax) * chartHeight;
          const y = chartHeight - barHeight;
          return `
            <rect class="customer-service-bar" x="${x}" y="${y}" width="${barWidth}" height="${barHeight}"></rect>
          `;
        }).join("")}
        ${rows.map((row, index) => {
          const x = index * (barWidth + barGap);
          const value = row.received ?? 0;
          const tooltipX = Math.min(Math.max(x + barWidth + 8, 0), chartWidth - 190);
          return `
            <g class="chart-hover-group">
              <rect class="customer-service-bar-hover" x="${x}" y="0" width="${barWidth}" height="${chartHeight}" tabindex="0"></rect>
              <line class="chart-hover-guide" x1="${x + (barWidth / 2)}" y1="0" x2="${x + (barWidth / 2)}" y2="${chartHeight}"></line>
              <foreignObject class="chart-tooltip-box customer-service-bar-tooltip-box" x="${tooltipX}" y="42" width="190" height="88">
                <div xmlns="http://www.w3.org/1999/xhtml" class="chart-tooltip approved-chart-tooltip">
                  <strong>${escapeHtml(row.month)}</strong>
                  <span><i class="legend-started"></i>PQRFS Recibidos <b>${gerenciaNumberFormatter.format(value)}</b></span>
                </div>
              </foreignObject>
            </g>
          `;
        }).join("")}
        ${rows.map((row, index) => {
          const x = index * (barWidth + barGap) + (barWidth / 2);
          const showLabel = index === 0 || index % 2 === 0;
          return `
            <line class="chart-x-tick" x1="${x}" y1="${chartHeight}" x2="${x}" y2="${chartHeight + 6}"></line>
            ${showLabel ? `<text class="chart-x-label" x="${x}" y="${chartHeight + 24}" text-anchor="middle">${escapeHtml(row.month)}</text>` : ""}
          `;
        }).join("")}
        <line class="chart-axis" x1="0" y1="${chartHeight}" x2="${chartWidth}" y2="${chartHeight}"></line>
      </g>
    </svg>
  `;
};

const getCustomerServiceResponseRequirements = () => [...new Set(filterRowsByGerenciaMonth(customerServiceResponseRows).map((row) => row.requirement))]
  .sort((a, b) => a.localeCompare(b, "es"));

const getFilteredCustomerServiceResponseRows = () => filterRowsByGerenciaMonth(customerServiceResponseRows);

const syncCustomerServiceActiveResponseRequirements = () => {
  const names = getCustomerServiceResponseRequirements();
  const valid = new Set(names);
  customerServiceActiveResponseRequirements = new Set([...customerServiceActiveResponseRequirements].filter((name) => valid.has(name)));

  if (!customerServiceActiveResponseRequirements.size && names.length) {
    customerServiceActiveResponseRequirements = new Set(names);
  }
};

const renderCustomerServiceResponseTable = () => {
  const head = document.getElementById("customerServiceResponseHead");
  const body = document.getElementById("customerServiceResponseRows");
  if (!head || !body) return;

  const filteredRows = getFilteredCustomerServiceResponseRows();
  const months = [...new Map(
    filteredRows
      .sort((a, b) => (a.monthNumber ?? 0) - (b.monthNumber ?? 0))
      .map((row) => [row.month, row])
  ).values()];
  const requirements = [...new Set(filteredRows.map((row) => row.requirement))]
    .sort((a, b) => a.localeCompare(b, "es"));

  if (!months.length || !requirements.length) {
    head.innerHTML = `<tr><th>Requerimiento</th></tr>`;
    body.innerHTML = `<tr><td>Sin registros de promedio de respuesta.</td></tr>`;
    return;
  }

  const byRequirementMonth = new Map();
  filteredRows.forEach((row) => {
    byRequirementMonth.set(`${row.requirement}__${row.month}`, row.average ?? 0);
  });

  head.innerHTML = `
    <tr>
      <th></th>
      <th>Metric</th>
      <th colspan="${months.length}">Promedio</th>
    </tr>
    <tr>
      <th>Requerimiento</th>
      <th>Meses</th>
      ${months.map((row) => `<th>${escapeHtml(row.month)}</th>`).join("")}
    </tr>
  `;

  body.innerHTML = requirements.map((requirement) => `
    <tr>
      <td>${escapeHtml(requirement)}</td>
      <td></td>
      ${months.map((month) => `<td>${gerenciaDecimalFormatter.format(byRequirementMonth.get(`${requirement}__${month.month}`) ?? 0)}</td>`).join("")}
    </tr>
  `).join("");
};

const renderCustomerServiceResponseLegend = () => {
  const legend = document.getElementById("customerServiceResponseLegend");
  if (!legend) return;

  const requirements = [...new Set(getFilteredCustomerServiceResponseRows().map((row) => row.requirement))]
    .sort((a, b) => a.localeCompare(b, "es"));
  legend.innerHTML = `
    <div class="bank-legend-series">
      ${requirements.map((requirement, index) => `
        <button class="bank-toggle ${customerServiceActiveResponseRequirements.has(requirement) ? "active" : ""}" type="button" data-customer-service-response-requirement="${escapeHtml(requirement)}">
          <i style="--bank-color: ${customerServiceColors[index % customerServiceColors.length]}"></i>${escapeHtml(requirement)}
        </button>
      `).join("")}
    </div>
    <div class="bank-legend-actions">
      <button class="legend-chip" type="button" data-customer-service-response-action="all">All</button>
      <button class="legend-chip" type="button" data-customer-service-response-action="invert">Inv</button>
    </div>
  `;
};

const renderCustomerServiceResponseChart = () => {
  const container = document.getElementById("customerServiceResponseChart");
  if (!container) return;

  syncCustomerServiceActiveResponseRequirements();
  renderCustomerServiceResponseLegend();

  const filteredRows = getFilteredCustomerServiceResponseRows();
  const months = [...new Map(
    filteredRows
      .sort((a, b) => (a.monthNumber ?? 0) - (b.monthNumber ?? 0))
      .map((row) => [row.month, row])
  ).values()];
  const requirements = [...new Set(filteredRows.map((row) => row.requirement))]
    .sort((a, b) => a.localeCompare(b, "es"))
    .filter((requirement) => customerServiceActiveResponseRequirements.has(requirement));

  if (!months.length || !requirements.length) {
    container.innerHTML = `<div class="chart-empty">Sin datos para graficar.</div>`;
    return;
  }

  const byRequirementMonth = new Map();
  filteredRows.forEach((row) => {
    byRequirementMonth.set(`${row.requirement}__${row.month}`, row.average ?? 0);
  });

  const width = 430;
  const height = 270;
  const padding = { top: 16, right: 24, bottom: 42, left: 48 };
  const chartWidth = width - padding.left - padding.right;
  const chartHeight = height - padding.top - padding.bottom;
  const values = requirements.flatMap((requirement) => months.map((month) => byRequirementMonth.get(`${requirement}__${month.month}`) ?? 0));
  const maxValue = Math.max(...values, 1);
  const scaleMax = Math.max(4, Math.ceil(maxValue / 2) * 2);
  const ticks = [0, .2, .4, .6, .8, 1].map((ratio) => scaleMax * ratio);

  const pointFor = (index, value) => ({
    x: months.length === 1 ? 0 : (index / (months.length - 1)) * chartWidth,
    y: chartHeight - ((value / scaleMax) * chartHeight)
  });

  container.innerHTML = `
    <svg viewBox="0 0 ${width} ${height}" role="img" aria-label="Promedio de días respuesta PQRFs">
      <g transform="translate(${padding.left} ${padding.top})">
        ${ticks.map((tick) => {
          const y = chartHeight - ((tick / scaleMax) * chartHeight);
          return `
            <line class="chart-grid-line" x1="0" y1="${y}" x2="${chartWidth}" y2="${y}"></line>
            <text class="chart-y-label" x="-10" y="${y + 4}" text-anchor="end">${gerenciaDecimalFormatter.format(tick)}</text>
          `;
        }).join("")}
        ${requirements.map((requirement, requirementIndex) => {
          const points = months.map((month, index) => {
            const value = byRequirementMonth.get(`${requirement}__${month.month}`) ?? 0;
            const point = pointFor(index, value);
            return `${point.x.toFixed(1)},${point.y.toFixed(1)}`;
          }).join(" ");

          return `<polyline class="line-meta customer-response-line-${requirementIndex % customerServiceColors.length}" style="stroke:${customerServiceColors[requirementIndex % customerServiceColors.length]}" points="${points}"></polyline>`;
        }).join("")}
        ${months.map((month, index) => {
          const x = months.length === 1 ? 0 : (index / (months.length - 1)) * chartWidth;
          const valuesByRequirement = requirements.map((requirement, requirementIndex) => ({
            requirement,
            value: byRequirementMonth.get(`${requirement}__${month.month}`) ?? 0,
            color: customerServiceColors[requirementIndex % customerServiceColors.length]
          }));
          const total = valuesByRequirement.reduce((sum, item) => sum + item.value, 0);
          const tooltipX = Math.min(Math.max(x + 10, 0), chartWidth - 188);
          return `
            <g class="chart-hover-group">
              <rect class="chart-hover-zone" x="${x - 18}" y="0" width="36" height="${chartHeight}" tabindex="0"></rect>
              <line class="chart-hover-guide" x1="${x}" y1="0" x2="${x}" y2="${chartHeight}"></line>
              ${valuesByRequirement.map((item) => {
                const point = pointFor(index, item.value);
                return `<circle class="chart-point" style="fill:${item.color}; stroke:${item.color}" cx="${point.x}" cy="${point.y}" r="3"></circle>`;
              }).join("")}
              <foreignObject class="chart-tooltip-box customer-service-response-tooltip-box" x="${tooltipX}" y="8" width="188" height="${Math.min(154, 40 + (valuesByRequirement.length * 20))}">
                <div xmlns="http://www.w3.org/1999/xhtml" class="chart-tooltip approved-chart-tooltip customer-service-response-tooltip">
                  <strong>${escapeHtml(month.month)}</strong>
                  ${valuesByRequirement.map((item) => `
                    <span><i style="background:${item.color}"></i>${escapeHtml(item.requirement)} <b>${gerenciaDecimalFormatter.format(item.value)}</b></span>
                  `).join("")}
                  <span class="tooltip-total-row">Total <b>${gerenciaDecimalFormatter.format(total)}</b></span>
                </div>
              </foreignObject>
            </g>
          `;
        }).join("")}
        ${months.map((month, index) => {
          const x = months.length === 1 ? 0 : (index / (months.length - 1)) * chartWidth;
          const showLabel = index === 0 || index % 2 === 0;
          return `
            <line class="chart-x-tick" x1="${x}" y1="${chartHeight}" x2="${x}" y2="${chartHeight + 6}"></line>
            ${showLabel ? `<text class="chart-x-label" x="${x}" y="${chartHeight + 24}" text-anchor="middle">${escapeHtml(month.month)}</text>` : ""}
          `;
        }).join("")}
        <line class="chart-axis" x1="0" y1="${chartHeight}" x2="${chartWidth}" y2="${chartHeight}"></line>
      </g>
    </svg>
  `;
};

const renderCustomerServiceResponseAverage = () => {
  renderCustomerServiceResponseTable();
  renderCustomerServiceResponseChart();
};

const loadCustomerServiceResponseAverage = async () => {
  const year = document.getElementById("gerenciaYear")?.value ?? "2026";
  const body = document.getElementById("customerServiceResponseRows");
  const chart = document.getElementById("customerServiceResponseChart");
  if (!body || !chart) return;

  try {
    const response = await fetch(`/api/reports/gerencia/servicio-cliente-promedio-respuesta?year=${encodeURIComponent(year)}`);
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    const data = await response.json();
    customerServiceResponseRows = data.rows ?? [];
    renderCustomerServiceResponseAverage();
  } catch (error) {
    body.innerHTML = `<tr><td>No fue posible cargar promedio de respuesta: ${error.message}</td></tr>`;
    chart.innerHTML = `<div class="chart-empty">No fue posible cargar gráfica de promedio: ${error.message}</div>`;
  }
};

const renderCustomerServiceWithdrawalSummary = (rows, headId, bodyId) => {
  const head = document.getElementById(headId);
  const body = document.getElementById(bodyId);
  if (!head || !body) return;

  const filteredRows = filterRowsByGerenciaMonth(rows);
  const months = [...new Map(
    filteredRows
      .filter((row) => row.month && row.month !== "13 OTRO")
      .sort((a, b) => (a.monthNumber ?? 0) - (b.monthNumber ?? 0))
      .map((row) => [row.month, row])
  ).values()];

  if (!months.length) {
    head.innerHTML = `<tr><th>Metric</th></tr>`;
    body.innerHTML = `<tr><td>Sin registros de desistimientos.</td></tr>`;
    return;
  }

  head.innerHTML = `
    <tr>
      <th></th>
      <th colspan="${months.length}">Meses</th>
    </tr>
    <tr>
      <th>Metric</th>
      ${months.map((row) => `<th>${escapeHtml(row.month)}</th>`).join("")}
    </tr>
  `;

  body.innerHTML = `
    <tr>
      <td>Casos radicados</td>
      ${months.map((row) => `<td>${gerenciaNumberFormatter.format(row.started ?? 0)}</td>`).join("")}
    </tr>
    <tr>
      <td>Casos desistidos</td>
      ${months.map((row) => `<td>${gerenciaNumberFormatter.format(row.withdrawn ?? 0)}</td>`).join("")}
    </tr>
  `;
};

const renderCustomerServiceWithdrawalDetail = (rows, bodyId) => {
  const body = document.getElementById(bodyId);
  if (!body) return;

  const sorted = filterRowsByGerenciaMonth(rows)
    .sort((a, b) => (a.monthNumber ?? 0) - (b.monthNumber ?? 0) || String(a.id ?? "").localeCompare(String(b.id ?? ""), "es"));

  if (!sorted.length) {
    body.innerHTML = `<tr><td colspan="5">Sin detalle de desistimientos.</td></tr>`;
    return;
  }

  body.innerHTML = sorted.map((row) => `
    <tr>
      <td>${escapeHtml(row.month)}</td>
      <td>${escapeHtml(row.id)}</td>
      <td>${escapeHtml(row.definitiveReason || "no seleccionado")}</td>
      <td>${escapeHtml(row.processLeader || "")}</td>
      <td>${row.refundValue == null ? "N/A" : gerenciaNumberFormatter.format(row.refundValue)}</td>
    </tr>
  `).join("");
};

const renderCustomerServiceWithdrawals = () => {
  renderCustomerServiceWithdrawalSummary(
    customerServiceWithdrawals.insolvencySummary ?? [],
    "customerServiceInsolvencyWithdrawalHead",
    "customerServiceInsolvencyWithdrawalRows"
  );
  renderCustomerServiceWithdrawalSummary(
    customerServiceWithdrawals.rchSummary ?? [],
    "customerServiceRchWithdrawalHead",
    "customerServiceRchWithdrawalRows"
  );
  renderCustomerServiceWithdrawalDetail(
    customerServiceWithdrawals.insolvencyDetail ?? [],
    "customerServiceInsolvencyWithdrawalDetailRows"
  );
  renderCustomerServiceWithdrawalDetail(
    customerServiceWithdrawals.rchDetail ?? [],
    "customerServiceRchWithdrawalDetailRows"
  );
};

const loadCustomerServiceWithdrawals = async () => {
  const year = document.getElementById("gerenciaYear")?.value ?? "2026";
  const anyTarget = document.getElementById("customerServiceInsolvencyWithdrawalRows");
  if (!anyTarget) return;

  try {
    const response = await fetch(`/api/reports/gerencia/servicio-cliente-desistimientos?year=${encodeURIComponent(year)}`);
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    customerServiceWithdrawals = await response.json();
    renderCustomerServiceWithdrawals();
  } catch (error) {
    ["customerServiceInsolvencyWithdrawalRows", "customerServiceRchWithdrawalRows"].forEach((id) => {
      const target = document.getElementById(id);
      if (target) target.innerHTML = `<tr><td>No fue posible cargar desistimientos: ${escapeHtml(error.message)}</td></tr>`;
    });
    ["customerServiceInsolvencyWithdrawalDetailRows", "customerServiceRchWithdrawalDetailRows"].forEach((id) => {
      const target = document.getElementById(id);
      if (target) target.innerHTML = `<tr><td colspan="5">No fue posible cargar detalle: ${escapeHtml(error.message)}</td></tr>`;
    });
  }
};

const renderCustomerServiceCharts = () => {
  renderCustomerServiceRequirementChart();
  renderCustomerServiceMonthlyChart();
};

const loadCustomerServiceCharts = async () => {
  const year = document.getElementById("gerenciaYear")?.value ?? "2026";
  const requirementContainer = document.getElementById("customerServiceRequirementChart");
  const monthlyContainer = document.getElementById("customerServiceMonthlyChart");
  if (!requirementContainer || !monthlyContainer) return;

  try {
    const response = await fetch(`/api/reports/gerencia/servicio-cliente-graficas?year=${encodeURIComponent(year)}`);
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    const data = await response.json();
    customerServiceRequirements = data.requirements ?? [];
    customerServiceMonthlyRows = data.monthly ?? [];
    renderCustomerServiceSummary();
    renderCustomerServiceCharts();
  } catch (error) {
    requirementContainer.innerHTML = `<div class="chart-empty">No fue posible cargar requerimientos: ${error.message}</div>`;
    monthlyContainer.innerHTML = `<div class="chart-empty">No fue posible cargar PQRFs por mes: ${error.message}</div>`;
  }
};

const handleCustomerServiceRequirementLegendClick = (event) => {
  const toggle = event.target.closest("[data-customer-service-requirement]");
  const action = event.target.closest("[data-customer-service-requirement-action]");

  if (toggle) {
    const requirement = toggle.dataset.customerServiceRequirement;
    if (customerServiceActiveRequirements.has(requirement)) {
      customerServiceActiveRequirements.delete(requirement);
    } else {
      customerServiceActiveRequirements.add(requirement);
    }
    renderCustomerServiceRequirementChart();
  }

  if (action) {
    const type = action.dataset.customerServiceRequirementAction;
    const names = getCustomerServiceRequirementTotals().map((item) => item.requirement);
    if (type === "all") {
      customerServiceActiveRequirements = new Set(names);
    } else if (type === "invert") {
      customerServiceActiveRequirements = new Set(names.filter((name) => !customerServiceActiveRequirements.has(name)));
      if (!customerServiceActiveRequirements.size) customerServiceActiveRequirements = new Set(names);
    }
    renderCustomerServiceRequirementChart();
  }
};

const handleCustomerServiceMonthlyLegendClick = (event) => {
  const series = event.target.closest("[data-customer-service-monthly-series]");
  const action = event.target.closest("[data-customer-service-monthly-action]");

  if (series) {
    customerServiceMonthlySeries.received = !customerServiceMonthlySeries.received;
  }

  if (action) {
    const type = action.dataset.customerServiceMonthlyAction;
    if (type === "all") {
      customerServiceMonthlySeries.received = true;
    } else if (type === "invert") {
      customerServiceMonthlySeries.received = !customerServiceMonthlySeries.received;
    }
  }

  renderCustomerServiceMonthlyChart();
};

const handleCustomerServiceResponseLegendClick = (event) => {
  const toggle = event.target.closest("[data-customer-service-response-requirement]");
  const action = event.target.closest("[data-customer-service-response-action]");

  if (toggle) {
    const requirement = toggle.dataset.customerServiceResponseRequirement;
    if (customerServiceActiveResponseRequirements.has(requirement)) {
      customerServiceActiveResponseRequirements.delete(requirement);
    } else {
      customerServiceActiveResponseRequirements.add(requirement);
    }
    renderCustomerServiceResponseChart();
  }

  if (action) {
    const type = action.dataset.customerServiceResponseAction;
    const names = getCustomerServiceResponseRequirements();
    if (type === "all") {
      customerServiceActiveResponseRequirements = new Set(names);
    } else if (type === "invert") {
      customerServiceActiveResponseRequirements = new Set(names.filter((name) => !customerServiceActiveResponseRequirements.has(name)));
      if (!customerServiceActiveResponseRequirements.size) customerServiceActiveResponseRequirements = new Set(names);
    }
    renderCustomerServiceResponseChart();
  }
};

const renderCustomerServiceSummary = () => {
  const complianceTarget = document.getElementById("customerServiceCompliance");
  const receivedTarget = document.getElementById("customerServiceReceived");
  if (!complianceTarget || !receivedTarget) return;

  const hasMonthFilter = hasGerenciaMonthFilter();
  const selectedRows = filterRowsByGerenciaMonth(customerServiceMonthlyRows);
  const selectedReceived = selectedRows.reduce((sum, row) => sum + Number(row.received ?? 0), 0);

  complianceTarget.textContent = gerenciaPercentFormatter.format(customerServiceSummary.compliance ?? 0);
  receivedTarget.textContent = gerenciaNumberFormatter.format(hasMonthFilter ? selectedReceived : (customerServiceSummary.received ?? 0));
};

const loadCustomerServiceSummary = async () => {
  const year = document.getElementById("gerenciaYear")?.value ?? "2026";
  const complianceTarget = document.getElementById("customerServiceCompliance");
  const receivedTarget = document.getElementById("customerServiceReceived");
  if (!complianceTarget || !receivedTarget) return;

  try {
    const response = await fetch(`/api/reports/gerencia/servicio-cliente-resumen?year=${encodeURIComponent(year)}`);
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    const data = await response.json();
    customerServiceSummary = {
      compliance: data.compliance ?? 0,
      received: data.received ?? 0
    };
    renderCustomerServiceSummary();
  } catch (error) {
    customerServiceSummary = { compliance: 0, received: 0 };
    complianceTarget.textContent = "0.0%";
    receivedTarget.textContent = "0";
  }
};

const renderAllGerenciaFilteredViews = () => {
  syncGerenciaMonthAllState();
  renderGerenciaMonthlyRows();
  if (!hasGerenciaMonthFilter()) loadGerenciaCompliance();
  renderPnncDetail();
  renderOperativaRchProcesses();
  renderOperativaRchApprovedTable();
  renderPnnc2025Processes();
  renderOperativaPnncManagement();
  renderOperativaPnncSecond();
  renderOperativaPnncDetail();
  renderLpMonthlyTasks();
  renderLpWeeklyTasks();
  renderLpEmbargosTasks();
  renderLpLibranzaTasks();
  renderInsEmbargosDetail();
  renderInsLibranzaDetail();
  renderInsuranceCompliance();
  renderInsuranceOperations();
  renderInsuranceOutOfTime();
  renderCustomerServiceSummary();
  renderCustomerServiceCharts();
  renderCustomerServiceResponseAverage();
  renderCustomerServiceWithdrawals();
};

const handleGerenciaMonthFilterChange = (event) => {
  const all = document.getElementById("gerenciaMonthAll");
  const monthInputs = [...document.querySelectorAll("[data-gerencia-month-filter]")];

  if (event.target === all && all.checked) {
    monthInputs.forEach((input) => {
      input.checked = false;
    });
  } else if (event.target?.matches?.("[data-gerencia-month-filter]") && all) {
    all.checked = false;
  }

  syncGerenciaMonthAllState();
  renderAllGerenciaFilteredViews();
};



const renderDiegoDashboard = () => {
  const sourceSections = diegoSections;
  const sections = sourceSections.map((section) => {
    const reportBlocks = section.blocks;
    return { ...section, blocks: reportId === "informe_general_comercial" && generalBlockAccess.configured
      ? reportBlocks.filter(([title]) => isGeneralBlockVisible(title))
      : reportBlocks };
  }).filter((section) => section.blocks.length);
  document.getElementById("diegoSections").innerHTML = sections.map((section) => `
    <section id="${section.id}" class="diego-section">
      <header>
        <span>${section.icon}</span>
        <div><h2>${section.title}</h2><p>${section.description}</p></div>
      </header>
      <div class="diego-block-grid ${section.blocks.length === 1 ? "is-single" : ""}">
        ${section.blocks.map(([title, description, type]) => `
          <article data-block-title="${title}" data-block-code="${generalBlockCodes[title]}" class="diego-block diego-block-${type}${["Total de negociaciones por asesor", "Cartera recaudada"].includes(title) ? " diego-block-wide-table" : ""}">
            <div class="diego-block-title"><div><h3>${title}</h3><p>${description}</p></div><em>Sin datos</em></div>
            ${blockPreview(type)}
          </article>
        `).join("")}
      </div>
    </section>
  `).join("");
  applyDiegoGridPacking();
};

const applyDiegoGridPacking = () => {
  document.querySelectorAll(".diego-block-grid").forEach((grid) => {
    const visibleBlocks = [...grid.querySelectorAll(".diego-block")].filter((block) => !block.hidden);
    visibleBlocks.forEach((block) => block.classList.remove("is-row-single"));
    const standardBlocks = visibleBlocks.filter((block) =>
      !block.classList.contains("diego-block-radicated")
      && !block.classList.contains("diego-block-wide-table")
      && !block.classList.contains("diego-block-commissions")
      && !block.classList.contains("diego-block-donut")
      && !block.classList.contains("diego-block-management-kpis")
      && !block.classList.contains("diego-block-management-close")
    );
    grid.classList.toggle("is-single", visibleBlocks.length === 1);
    if (standardBlocks.length % 2 === 1) {
      standardBlocks[standardBlocks.length - 1].classList.add("is-row-single");
    }
  });
};

const loadGerenciaReportData = async () => {
  const status = document.getElementById("gerenciaSyncStatus");
  if (status) {
    status.className = "gerencia-sync-status is-loading";
    status.textContent = "Actualizando información…";
  }
  const loaders = [loadGerenciaCompliance, loadGerenciaMonthlyCompliance, loadGerenciaPossibleClose, loadPnncDetailCompliance, loadRchAccumulatedAverage, loadOperativaRchProcesses, loadOperativaRchApprovedByBank, loadPnnc2025Processes, loadOperativaPnncManagement, loadOperativaPnncSecond, loadOperativaPnncDetail, loadPnncLpCompliance2025, loadLpMonthlyTasks, loadLpWeeklyTasks, loadLpEmbargosTasks, loadLpLibranzaTasks, loadInsEmbargosDetail, loadInsLibranzaDetail, loadInsuranceCompliance, loadInsuranceOperations, loadInsuranceOutOfTime, loadCustomerServiceSummary, loadCustomerServiceCharts, loadCustomerServiceResponseAverage, loadCustomerServiceWithdrawals];
  await Promise.allSettled(loaders.map((loader) => loader()));
  const hasVisibleErrors = /No fue posible|HTTP 5\d\d/i.test(document.getElementById("gerenciaDashboard")?.innerText ?? "");
  const hasCommercialData = gerenciaMonthlyRows.length > 0;
  if (status) {
    status.className = `gerencia-sync-status ${hasVisibleErrors || !hasCommercialData ? "has-warning" : "is-ready"}`;
    status.textContent = hasVisibleErrors
      ? "Carga parcial · revisa los módulos señalados"
      : !hasCommercialData
        ? `Sin datos comerciales sincronizados para ${document.getElementById("gerenciaYear")?.value ?? "el periodo"}`
      : `Datos actualizados · ${new Date().toLocaleTimeString("es-CO", { hour: "2-digit", minute: "2-digit" })}`;
  }
};

const setText = (id, value) => {
  document.getElementById(id).textContent = value;
};

const formatNumber = new Intl.NumberFormat("es-CO");
let standardDeals = [];

const normalizeFilterValue = (value) => String(value ?? "")
  .normalize("NFD")
  .replace(/[\u0300-\u036f]/g, "")
  .toLowerCase()
  .trim();

const searchableFilterSelectIds = new Set([
  "standardStageFilter",
  "standardOwnerFilter",
  "diegoMonth",
  "diegoCoordinator",
  "diegoLeader",
  "diegoAdvisor"
]);

const getFilterSelectLabel = (select) => {
  const label = select.closest("label");
  const text = [...(label?.childNodes ?? [])]
    .filter((node) => node.nodeType === Node.TEXT_NODE)
    .map((node) => node.textContent.trim())
    .join(" ")
    .trim();
  return text || "opciones";
};

const getSearchableSelectInput = (select) =>
  select.closest("label")?.querySelector(`.filter-combobox-input[data-target-select="${select.id}"]`);

const getSearchableSelectRoot = (select) =>
  select.closest("label")?.querySelector(`.filter-combobox[data-target-select="${select.id}"]`);

const getSearchableSelectDropdown = (select) =>
  select.closest("label")?.querySelector(`.filter-combobox-list[data-target-select="${select.id}"]`);

let searchableSelectDocumentBound = false;

const searchableSelectOptions = (select) => select._filterOptions ?? [...select.options].map((option) => ({
  value: option.value,
  label: option.textContent
}));

const closeFilterComboboxes = (exceptRoot = null) => {
  document.querySelectorAll(".filter-combobox.is-open").forEach((root) => {
    if (root === exceptRoot) return;
    const select = document.getElementById(root.dataset.targetSelect);
    if (select) syncSearchableSelectInput(select);
    root.classList.remove("is-open");
  });
};

const syncSearchableSelectInput = (select) => {
  const options = searchableSelectOptions(select);
  const input = getSearchableSelectInput(select);
  if (!input) return;
  if (isMultiFilterSelect(select)) {
    const values = selectedFilterValues(select);
    const labels = options
      .filter((option) => values.includes(option.value))
      .map((option) => option.label);
    input.value = labels.length === 0
      ? (options[0]?.label ?? "Todos")
      : labels.length <= 2
        ? labels.join(", ")
        : `${labels.length} seleccionados`;
    return;
  }
  const selected = options.find((option) => option.value === select.value) ?? options[0];
  input.value = selected?.label ?? "";
};

const applySearchableSelectFilter = (select) => {
  const root = getSearchableSelectRoot(select);
  const input = getSearchableSelectInput(select);
  const dropdown = getSearchableSelectDropdown(select);
  if (!root || !input || !dropdown) return;

  const options = searchableSelectOptions(select);
  const selected = options.find((option) => option.value === select.value);
  const selectedValues = isMultiFilterSelect(select) ? selectedFilterValues(select) : [];
  const selectedLabels = isMultiFilterSelect(select)
    ? options.filter((option) => selectedValues.includes(option.value)).map((option) => option.label)
    : [];
  const multiDisplayValue = selectedLabels.length === 0
    ? (options[0]?.label ?? "Todos")
    : selectedLabels.length <= 2
      ? selectedLabels.join(", ")
      : `${selectedLabels.length} seleccionados`;
  const inputValue = normalizeFilterValue(input.value);
  const term = (select.value === "all" && inputValue === normalizeFilterValue(selected?.label ?? ""))
    || (isMultiFilterSelect(select) && inputValue === normalizeFilterValue(multiDisplayValue))
    ? ""
    : inputValue;
  const visibleOptions = options.filter((option, index) =>
    index === 0 || !term || normalizeFilterValue(option.label).includes(term)
  );
  const isOptionSelected = (option) => isMultiFilterSelect(select)
    ? (option.value === "all" ? selectedValues.length === 0 : selectedValues.includes(option.value))
    : option.value === select.value;

  dropdown.innerHTML = visibleOptions.length
    ? visibleOptions.map((option) => `
      <button type="button" data-value="${escapeHtml(option.value)}" class="${isOptionSelected(option) ? "is-selected" : ""}">
        ${escapeHtml(option.label)}
      </button>`).join("")
    : `<span class="filter-combobox-empty">Sin coincidencias</span>`;
  root.classList.add("is-open");
};

const enhanceSearchableFilterSelect = (select) => {
  if (!select || !searchableFilterSelectIds.has(select.id)) return;
  const label = select.closest("label");
  if (!label) return;

  if (!getSearchableSelectInput(select)) {
    const combo = document.createElement("span");
    combo.className = "filter-combobox";
    combo.dataset.targetSelect = select.id;
    combo.innerHTML = `
      <input class="filter-combobox-input" type="search" autocomplete="off" data-target-select="${select.id}" placeholder="${getFilterSelectLabel(select)}">
      <span class="filter-combobox-list" data-target-select="${select.id}" role="listbox"></span>`;
    label.insertBefore(combo, select);
    select.classList.add("filter-combobox-native");

    const input = getSearchableSelectInput(select);
    const dropdown = getSearchableSelectDropdown(select);
    input.addEventListener("focus", () => {
      closeFilterComboboxes(combo);
      if (isMultiFilterSelect(select) || select.value === "all") input.value = "";
      else input.select();
      applySearchableSelectFilter(select);
    });
    input.addEventListener("input", () => applySearchableSelectFilter(select));
    input.addEventListener("keydown", (event) => {
      if (event.key === "Escape") {
        combo.classList.remove("is-open");
        syncSearchableSelectInput(select);
      }
      if (event.key === "Enter") {
        event.preventDefault();
        const first = dropdown.querySelector("button:not([data-value='all'])") ?? dropdown.querySelector("button");
        first?.click();
      }
    });
    dropdown.addEventListener("mousedown", (event) => event.preventDefault());
    dropdown.addEventListener("click", (event) => {
      event.stopPropagation();
      const option = event.target.closest("button[data-value]");
      if (!option) return;
      if (isMultiFilterSelect(select)) {
        const nativeOption = [...select.options].find((item) => item.value === option.dataset.value);
        if (!nativeOption) return;
        if (nativeOption.value === "all") {
          [...select.options].forEach((item) => {
            item.selected = item.value === "all";
          });
        } else {
          nativeOption.selected = !nativeOption.selected;
          const selectedSpecificOptions = [...select.options].filter((item) => item.value !== "all" && item.selected);
          const allOption = [...select.options].find((item) => item.value === "all");
          if (allOption) allOption.selected = selectedSpecificOptions.length === 0;
        }
        select.dispatchEvent(new Event("change", { bubbles: true }));
        input.value = "";
        applySearchableSelectFilter(select);
        return;
      }
      select.value = option.dataset.value;
      syncSearchableSelectInput(select);
      combo.classList.remove("is-open");
      select.dispatchEvent(new Event("change", { bubbles: true }));
    });
    select.addEventListener("change", () => {
      syncSearchableSelectInput(select);
    });
  }

  select._filterOptions = [...select.options].map((option) => ({
    value: option.value,
    label: option.textContent
  }));
  const input = getSearchableSelectInput(select);
  if (input) input.disabled = select.disabled;
  syncSearchableSelectInput(select);
  if (!searchableSelectDocumentBound) {
    document.addEventListener("click", (event) => {
      if (!event.target.closest(".filter-combobox")) closeFilterComboboxes();
    });
    searchableSelectDocumentBound = true;
  }
};

const focusFirstFilterSearch = (panel) => {
  const search = panel?.querySelector(".filter-combobox-input:not(:disabled)");
  if (search) window.setTimeout(() => search.focus(), 60);
};

const clearFilterOptionSearches = (ids) => {
  ids.forEach((id) => {
    const select = document.getElementById(id);
    const input = select ? getSearchableSelectInput(select) : null;
    if (!select || !input) return;
    syncSearchableSelectInput(select);
    getSearchableSelectRoot(select)?.classList.remove("is-open");
  });
};

const setupFilterDrawer = (panel, toggle) => {
  document.body.classList.add("report-has-filter-drawer");
  const setCollapsed = (collapsed) => {
    panel.classList.toggle("is-collapsed", collapsed);
    document.body.classList.toggle("filter-panel-collapsed", collapsed);
    toggle.setAttribute("aria-expanded", String(!collapsed));
    toggle.querySelector("b").textContent = collapsed ? "Mostrar filtros" : "Ocultar filtros";
    toggle.title = collapsed ? "Mostrar filtros" : "Ocultar filtros";
  };
  setCollapsed(false);
  toggle.addEventListener("click", () => {
    const collapsed = !panel.classList.contains("is-collapsed");
    setCollapsed(collapsed);
    if (!collapsed) focusFirstFilterSearch(panel);
  });
};

const fillStandardFilter = (select, values, allLabel) => {
  select.innerHTML = `<option value="all">${allLabel}</option>`;
  [...new Set(values.filter(Boolean))]
    .sort((left, right) => left.localeCompare(right, "es", { sensitivity: "base" }))
    .forEach((value) => select.add(new Option(value, value)));
  enhanceSearchableFilterSelect(select);
};

const renderStandardDistributions = (deals) => {
  const countBy = (selector) => deals.reduce((counts, deal) => {
    const label = selector(deal) || "Sin asignar";
    counts.set(label, (counts.get(label) ?? 0) + 1);
    return counts;
  }, new Map());
  const stages = [...countBy((deal) => deal.stageName ?? deal.stageId).entries()]
    .sort((left, right) => right[1] - left[1]);
  const owners = [...countBy((deal) => deal.responsibleName).entries()]
    .sort((left, right) => right[1] - left[1]);
  const maxStage = Math.max(...stages.map(([, count]) => count), 1);

  document.getElementById("stageBars").innerHTML = stages.map(([stageName, dealsCount]) => `
    <div class="stage-row">
      <span>${stageName}</span>
      <div><i style="width:${Math.max(8, (dealsCount / maxStage) * 100)}%"></i></div>
      <b>${dealsCount}</b>
    </div>
  `).join("");
  document.getElementById("ownerList").innerHTML = owners.map(([responsibleName, dealsCount]) => `
    <div class="owner-row"><span>${responsibleName}</span><b>${dealsCount}</b></div>
  `).join("");
};

const applyStandardFilters = () => {
  const search = normalizeFilterValue(document.getElementById("standardDealSearch").value);
  const stage = document.getElementById("standardStageFilter").value;
  const owner = document.getElementById("standardOwnerFilter").value;
  const filtered = standardDeals.filter((deal) => {
    const searchable = normalizeFilterValue(`${deal.bitrixId} ${deal.title}`);
    const dealStage = deal.stageName ?? deal.stageId ?? "";
    const dealOwner = deal.responsibleName ?? "";
    return (!search || searchable.includes(search))
      && (stage === "all" || dealStage === stage)
      && (owner === "all" || dealOwner === owner);
  });

  document.getElementById("dealRows").innerHTML = filtered.map((deal) => `
    <tr>
      <td>${deal.bitrixId}</td>
      <td><strong>${deal.title}</strong></td>
      <td>${deal.stageName ?? deal.stageId ?? ""}</td>
      <td>${deal.responsibleName ?? ""}</td>
      <td>${deal.opportunity ?? ""}</td>
      <td>${deal.currencyId ?? ""}</td>
    </tr>
  `).join("");
  setText("summaryDeals", formatNumber.format(filtered.length));
  setText("summaryStages", formatNumber.format(new Set(filtered.map((deal) => deal.stageName ?? deal.stageId).filter(Boolean)).size));
  setText("summaryUsers", formatNumber.format(new Set(filtered.map((deal) => deal.responsibleName).filter(Boolean)).size));
  renderStandardDistributions(filtered);
};

const setupStandardFilters = () => {
  const panel = document.getElementById("standardFilters");
  panel.hidden = false;
  fillStandardFilter(document.getElementById("standardStageFilter"), standardDeals.map((deal) => deal.stageName ?? deal.stageId), "Todas");
  fillStandardFilter(document.getElementById("standardOwnerFilter"), standardDeals.map((deal) => deal.responsibleName), "Todos");
  if (!panel.dataset.ready) {
    ["standardStageFilter", "standardOwnerFilter"].forEach((id) => document.getElementById(id).addEventListener("change", applyStandardFilters));
    document.getElementById("standardDealSearch").addEventListener("input", applyStandardFilters);
    document.getElementById("clearStandardFilters").addEventListener("click", () => {
      document.getElementById("standardDealSearch").value = "";
      document.getElementById("standardStageFilter").value = "all";
      document.getElementById("standardOwnerFilter").value = "all";
      clearFilterOptionSearches(["standardStageFilter", "standardOwnerFilter"]);
      applyStandardFilters();
    });
    setupFilterDrawer(panel, document.getElementById("toggleStandardFilters"));
    panel.dataset.ready = "true";
  }
  applyStandardFilters();
};

const loadSummary = async () => {
  const response = await fetch(`/api/data/sync-summary?pipeline=${reportId}`);
  const summary = await response.json();
  setText("summaryDeals", formatNumber.format(summary.dealsCount ?? 0));
  setText("summaryStages", formatNumber.format(summary.stagesCount ?? 0));
  setText("summaryUsers", formatNumber.format(summary.usersCount ?? 0));
  setText("summaryStatus", summary.lastSync?.status ?? "-");
  setText("summaryLastRun", summary.lastSync ? `${summary.lastSync.recordsWritten} escritos` : "Sin datos");
};

const formatSyncDateTime = (value) => {
  if (!value) return null;
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return null;
  return new Intl.DateTimeFormat("es-CO", {
    timeZone: "America/Bogota",
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
    hour12: true
  }).format(date);
};

const loadDiegoSyncStatus = async () => {
  const target = document.getElementById("diegoSyncStatusText");
  if (!target) return;
  try {
    const response = await fetch("/api/data/sync-history", { cache: "no-store" });
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    const rows = await response.json();
    const completed = rows.filter((row) => row.status === "success" && (row.finishedAt || row.createdAt));
    const penultimateCompleted = completed[1] ?? completed[0]
      ?? rows.filter((row) => row.finishedAt || row.createdAt)[1]
      ?? rows.find((row) => row.finishedAt || row.createdAt);
    const time = formatSyncDateTime(penultimateCompleted?.finishedAt ?? penultimateCompleted?.createdAt);
    target.textContent = time ? `Penúltima actualización: ${time}` : "Sin actualización registrada";
    target.dateTime = penultimateCompleted?.finishedAt ?? penultimateCompleted?.createdAt ?? "";
  } catch {
    target.textContent = "Actualización no disponible";
    target.removeAttribute("datetime");
  }
};

const loadDeals = async () => {
  const response = await fetch(`/api/data/deals?pipeline=${reportId}`);
  standardDeals = await response.json();
  setupStandardFilters();
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
    // Safari can restore date values from the previous page state even when
    // the HTML inputs have no value. A restored single-day range makes the
    // report look empty, so commercial reports always start with the full
    // selected year unless the user explicitly chooses a range afterwards.
    document.getElementById("diegoDateFrom").value = "";
    document.getElementById("diegoDateTo").value = "";
    commercialDateRangeTouched = false;
    const session = await fetch("/api/auth/me").then((response) => response.json());
    teamScope = session.roleCode === "admin"
      ? null
      : (session.teamScope ?? { departmentNames: [], memberNames: [] });
    if (isGeneralCommercial) {
      generalBlockAccess = { configured: Boolean(session.generalCommercialBlocksConfigured), codes: new Set(session.generalCommercialBlockCodes ?? []) };
    }
    document.body.classList.toggle("general-commercial-report", isGeneralCommercial);
    document.querySelector(".compact-hero").hidden = true;
    document.getElementById("standardSummary").hidden = true;
    document.getElementById("standardVisuals").hidden = true;
    document.getElementById("detalle").hidden = true;
    document.getElementById("gerenciaDashboard").hidden = true;
    document.getElementById("diegoDashboard").hidden = false;
    if (isGeneralCommercial) {
      document.title = "Informe General Comercial | Avanzar";
      document.querySelector(".diego-overview-kicker").textContent = "Panel general · Información consolidada desde Bitrix";
      document.querySelector(".diego-overview h2").textContent = "Informe general del área comercial";
      document.querySelector(".diego-overview p").textContent = "Consulta radicación, negociaciones, comisiones, cartera, embudos y etapas sincronizadas desde Bitrix.";
      document.getElementById("diegoYearFilter").hidden = true;
    }
    renderDiegoDashboard();
    try {
      await loadDiegoFilterHierarchy();
    } catch (error) {
      console.warn("La jerarquía comercial aún no está disponible; el informe continuará cargando sus tablas.", error);
      commercialHierarchy = [];
    }
    document.getElementById("clearDiegoFilters").addEventListener("click", clearDiegoFilters);
    // There is also a reusable standard filter panel in the document. Scope
    // the lookup to the active commercial dashboard so the toggle changes the
    // drawer the user can actually see.
    const filterPanel = document.querySelector("#diegoDashboard .diego-filters");
    const filterToggle = document.getElementById("toggleDiegoFilters");
    setupFilterDrawer(filterPanel, filterToggle);
    document.getElementById("diegoYear").addEventListener("change", markCommercialViewPending);
    setText("reportStatus", "Leyendo");
    await loadDiegoSyncStatus();
    await reloadDiegoCommercialData();
    startDiegoAutoRefresh();
    setText("reportStatus", "OK");
    return;
  }
  if (reportId === "informe_gerencia_2026_2027") {
    document.getElementById("standardSummary").hidden = true;
    document.getElementById("standardVisuals").hidden = true;
    document.getElementById("detalle").hidden = true;
    document.getElementById("diegoDashboard").hidden = true;
    document.getElementById("gerenciaDashboard").hidden = false;
    setupFilterDrawer(document.querySelector(".gerencia-filters"), document.getElementById("toggleGerenciaFilters"));
    document.getElementById("clearGerenciaFilters").addEventListener("click", () => {
      document.getElementById("gerenciaYear").value = "2026";
      document.getElementById("gerenciaMonthAll").checked = true;
      document.querySelectorAll("[data-gerencia-month-filter]").forEach((checkbox) => { checkbox.checked = false; });
      document.getElementById("gerenciaMonthDropdown").removeAttribute("open");
      syncGerenciaMonthAllState();
      document.getElementById("gerenciaYear").dispatchEvent(new Event("change"));
    });
    document.querySelector(".menu").innerHTML = `
      <p>GENERAL</p>
      <a href="/"><span></span>Inicio</a>
      <a href="/informes.html"><span></span>Informes</a>
      <p>VISTAS</p>
      <a class="active" href="#"><span>G</span>Cumplimiento</a>`;
    document.querySelectorAll("[data-gerencia-sort]").forEach((button) => {
      button.addEventListener("click", () => cycleGerenciaSort(button.dataset.gerenciaSort));
    });
    document.querySelectorAll("[data-pnnc-sort]").forEach((button) => {
      button.addEventListener("click", () => cyclePnncSort(button.dataset.pnncSort));
    });
    document.querySelectorAll("[data-rch-operativa-sort]").forEach((button) => {
      button.addEventListener("click", () => cycleOperativaRchSort(button.dataset.rchOperativaSort));
    });
    document.querySelectorAll("[data-pnnc-operativa-sort]").forEach((button) => {
      button.addEventListener("click", () => cycleOperativaPnncSort(button.dataset.pnncOperativaSort));
    });
    document.querySelectorAll("[data-pnnc-operativa2-sort]").forEach((button) => {
      button.addEventListener("click", () => cycleOperativaPnncSecondSort(button.dataset.pnncOperativa2Sort));
    });
    document.querySelectorAll("[data-pnnc-operativa-detail-sort]").forEach((button) => {
      button.addEventListener("click", () => cycleOperativaPnncDetailSort(button.dataset.pnncOperativaDetailSort));
    });
    document.querySelectorAll("[data-lp-monthly-sort]").forEach((button) => {
      button.addEventListener("click", () => {
        lpMonthlyTaskSort = cycleSort(lpMonthlyTaskSort, button.dataset.lpMonthlySort, button.dataset.lpMonthlySort === "month" ? "asc" : "desc");
        renderLpMonthlyTasks();
      });
    });
    document.querySelectorAll("[data-lp-weekly-sort]").forEach((button) => {
      button.addEventListener("click", () => {
        lpWeeklyTaskSort = cycleSort(lpWeeklyTaskSort, button.dataset.lpWeeklySort, button.dataset.lpWeeklySort === "week" ? "asc" : "desc");
        renderLpWeeklyTasks();
      });
    });
    document.querySelectorAll("[data-lp-embargos-sort]").forEach((button) => {
      button.addEventListener("click", () => {
        lpEmbargosTaskSort = cycleSort(lpEmbargosTaskSort, button.dataset.lpEmbargosSort, button.dataset.lpEmbargosSort === "month" ? "asc" : "desc");
        renderLpEmbargosTasks();
      });
    });
    document.querySelectorAll("[data-lp-libranza-sort]").forEach((button) => {
      button.addEventListener("click", () => {
        lpLibranzaTaskSort = cycleSort(lpLibranzaTaskSort, button.dataset.lpLibranzaSort, button.dataset.lpLibranzaSort === "month" ? "asc" : "desc");
        renderLpLibranzaTasks();
      });
    });
    document.querySelectorAll("[data-ins-embargos-sort]").forEach((button) => {
      button.addEventListener("click", () => {
        insEmbargosSort = cycleSort(insEmbargosSort, button.dataset.insEmbargosSort, button.dataset.insEmbargosSort === "name" ? "asc" : "desc");
        renderInsEmbargosDetail();
      });
    });
    document.querySelectorAll("[data-ins-libranza-sort]").forEach((button) => {
      button.addEventListener("click", () => {
        insLibranzaSort = cycleSort(insLibranzaSort, button.dataset.insLibranzaSort, button.dataset.insLibranzaSort === "name" ? "asc" : "desc");
        renderInsLibranzaDetail();
      });
    });
    document.querySelectorAll("[data-insurance-kpi-sort]").forEach((button) => {
      button.addEventListener("click", () => {
        const key = button.dataset.insuranceKpiSort;
        insuranceKpiSort = cycleSort(insuranceKpiSort, key, key === "monthNumber" ? "asc" : "desc");
        renderInsuranceCompliance();
      });
    });
    document.querySelectorAll("[data-insurance-commercial-sort]").forEach((button) => {
      button.addEventListener("click", () => {
        const key = button.dataset.insuranceCommercialSort;
        insuranceCommercialSort = cycleSort(insuranceCommercialSort, key, key === "monthNumber" ? "asc" : "desc");
        renderInsuranceCompliance();
      });
    });
    document.querySelectorAll("[data-insurance-calls-sort]").forEach((button) => {
      button.addEventListener("click", () => {
        const key = button.dataset.insuranceCallsSort;
        insuranceCallsSort = cycleSort(insuranceCallsSort, key, key === "monthNumber" ? "asc" : "desc");
        renderInsuranceOperations();
      });
    });
    document.querySelectorAll("[data-insurance-quotes-sort]").forEach((button) => {
      button.addEventListener("click", () => {
        const key = button.dataset.insuranceQuotesSort;
        insuranceQuotesSort = cycleSort(insuranceQuotesSort, key, key === "monthNumber" ? "asc" : "desc");
        renderInsuranceOperations();
      });
    });
    document.querySelectorAll("[data-insurance-out-sort]").forEach((button) => {
      button.addEventListener("click", () => {
        const key = button.dataset.insuranceOutSort;
        insuranceOutSort = cycleSort(insuranceOutSort, key, key === "stage" ? "asc" : "desc");
        renderInsuranceOutOfTime();
      });
    });
    document.querySelectorAll("[data-insurance-out-detail-sort]").forEach((button) => {
      button.addEventListener("click", () => {
        const key = button.dataset.insuranceOutDetailSort;
        insuranceOutDetailSort = cycleSort(insuranceOutDetailSort, key, key === "name" || key === "stage" ? "asc" : "desc");
        renderInsuranceOutOfTime();
      });
    });
    document.getElementById("insuranceOutDetailSearch")?.addEventListener("input", (event) => {
      insuranceOutDetailSearch = event.target.value;
      renderInsuranceOutOfTime();
    });
    document.querySelectorAll("[data-chart-series]").forEach((button) => {
      button.addEventListener("click", () => setGerenciaChartSeries(button.dataset.chartSeries));
    });
    document.querySelectorAll("[data-chart-action]").forEach((button) => {
      button.addEventListener("click", () => runGerenciaChartAction(button.dataset.chartAction));
    });
    document.querySelectorAll("[data-pnnc-chart-series]").forEach((button) => {
      button.addEventListener("click", () => setPnncChartSeries(button.dataset.pnncChartSeries));
    });
    document.querySelectorAll("[data-pnnc-chart-action]").forEach((button) => {
      button.addEventListener("click", () => runPnncChartAction(button.dataset.pnncChartAction));
    });
    document.querySelectorAll("[data-rch-operativa-chart-series]").forEach((button) => {
      button.addEventListener("click", () => setOperativaRchChartSeries(button.dataset.rchOperativaChartSeries));
    });
    document.querySelectorAll("[data-rch-operativa-chart-action]").forEach((button) => {
      button.addEventListener("click", () => runOperativaRchChartAction(button.dataset.rchOperativaChartAction));
    });
    document.querySelectorAll("[data-rch-approved-chart-series]").forEach((button) => {
      button.addEventListener("click", () => {
        operativaRchApprovedChartSeries = { amount: true };
        renderOperativaRchApprovedChart();
      });
    });
    document.querySelectorAll("[data-rch-approved-chart-action]").forEach((button) => {
      button.addEventListener("click", () => runOperativaRchApprovedChartAction(button.dataset.rchApprovedChartAction));
    });
    document.querySelectorAll("[data-pnnc-2025-process-sort]").forEach((button) => {
      button.addEventListener("click", () => cyclePnnc2025ProcessSort(button.getAttribute("data-pnnc-2025-process-sort")));
    });
    document.querySelectorAll("[data-pnnc-2025-process-chart-series]").forEach((button) => {
      button.addEventListener("click", () => setPnnc2025ProcessChartSeries(button.getAttribute("data-pnnc-2025-process-chart-series")));
    });
    document.querySelectorAll("[data-pnnc-2025-process-chart-action]").forEach((button) => {
      button.addEventListener("click", () => runPnnc2025ProcessChartAction(button.getAttribute("data-pnnc-2025-process-chart-action")));
    });
    document.getElementById("operativaRchBankLegend").addEventListener("click", handleOperativaRchBankLegendClick);
    document.getElementById("customerServiceRequirementLegend")?.addEventListener("click", handleCustomerServiceRequirementLegendClick);
    document.getElementById("customerServiceMonthlyLegend")?.addEventListener("click", handleCustomerServiceMonthlyLegendClick);
    document.getElementById("customerServiceResponseLegend")?.addEventListener("click", handleCustomerServiceResponseLegendClick);
    document.getElementById("operativaPnncSearch").addEventListener("input", renderOperativaPnncManagement);
    document.getElementById("gerenciaMonthGroup")?.addEventListener("change", handleGerenciaMonthFilterChange);
    document.getElementById("gerenciaYear").addEventListener("change", async () => {
      await loadGerenciaReportData();
    });
    await loadGerenciaReportData();
    return;
  }
  await loadSummary();
  await loadDeals();
};

const updateReportView = async () => {
  setText("reportStatus", "Leyendo");
  window.location.reload();
};

const setupSidebarToggle = () => {
  const button = document.getElementById("toggleSidebar");
  if (!button) return;

  const storageKey = "avanzar.sidebarCollapsed";
  const applyState = (collapsed) => {
    const label = collapsed ? "Mostrar menu" : "Ocultar menu";
    document.body.classList.toggle("sidebar-collapsed", collapsed);
    button.setAttribute("aria-expanded", String(!collapsed));
    button.setAttribute("aria-label", label);
    button.title = label;
    const text = button.querySelector("b");
    if (text) text.textContent = collapsed ? "Abrir menu" : "Cerrar menu";
  };

  applyState(localStorage.getItem(storageKey) === "true");
  button.addEventListener("click", () => {
    const collapsed = !document.body.classList.contains("sidebar-collapsed");
    localStorage.setItem(storageKey, String(collapsed));
    applyState(collapsed);
  });
};

setupSidebarToggle();
document.getElementById("refreshReportButton").addEventListener("click", updateReportView);

let totalsDecorationFrame = 0;
const scheduleAllTableTotals = () => {
  cancelAnimationFrame(totalsDecorationFrame);
  totalsDecorationFrame = requestAnimationFrame(() => decorateTableTotals(document.querySelector("main") ?? document));
};
const reportTotalsObserver = new MutationObserver((mutations) => {
  if (mutations.some((mutation) => [...mutation.addedNodes].some((node) => node.nodeType === Node.ELEMENT_NODE && (node.matches?.("table, tbody, tr") || node.querySelector?.("table"))))) {
    scheduleAllTableTotals();
  }
});
reportTotalsObserver.observe(document.querySelector("main") ?? document.body, { childList: true, subtree: true });

load().finally(scheduleAllTableTotals);
