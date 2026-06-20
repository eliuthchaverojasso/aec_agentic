export const demoFallback = {
  source: "demo_fallback",
  milestone: {
    currentStage: "DD 75%",
    nextStage: "DD 95%",
    dueDate: "May 12, 2026",
    daysToDeliverable: 18,
    stageLabel: "Construction Documents",
  },
  readiness: {
    drawingRequirements: 78,
    specRequirements: 81,
    drawingPackage: 68,
    drawingPackageGaps: 89,
    projectsDueSoon: 1,
    panelScheduleIssues: 127,
  },
  evidenceByDiscipline: {
    ELECTRICAL: "Model element + sheet reference",
    LIGHTING: "Model element + sheet reference",
    TECHNOLOGY: "Model element + sheet reference",
    MECHANICAL: "Model parameter + specification reference",
    PLUMBING: "Model parameter + specification reference",
  },
  sheetByDiscipline: {
    ELECTRICAL: "E2.01",
    LIGHTING: "E4.01",
    MECHANICAL: "M2.01",
    PLUMBING: "P2.01",
    TECHNOLOGY: "T2.01",
  },
  compliance: [
    { label: "IBC 2021", value: 68 },
    { label: "NYC Building Code", value: 74 },
    { label: "NFPA 101", value: 92 },
    { label: "ADA Compliance", value: 95 },
    { label: "Energy Code", value: 88 },
  ],
};

export const demoMilestone = {
  ...demoFallback.milestone,
  drawingRequirements: demoFallback.readiness.drawingRequirements,
  specRequirements: demoFallback.readiness.specRequirements,
  drawingPackage: demoFallback.readiness.drawingPackage,
  source: demoFallback.source,
};
