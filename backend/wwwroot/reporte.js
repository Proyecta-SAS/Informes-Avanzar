const params = new URLSearchParams(window.location.search);
const reportId = params.get("id") ?? "rch_comercial";

const metadata = {
  rch_comercial: { name: "RCH Comercial", area: "Comercial", description: "Seguimiento comercial de negociaciones RCH." },
  rch_operativa: { name: "RCH Operativa", area: "Operaciones", description: "Seguimiento operativo de negociaciones RCH." },
  pnnc_comercial: { name: "PNNC Comercial", area: "Comercial", description: "Dashboard comercial PNNC." },
  pnnc_operativa: { name: "PNNC Operativa", area: "Operaciones", description: "Dashboard operativo PNNC." }
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
  await loadSummary();
  await loadDeals();
};

load();
