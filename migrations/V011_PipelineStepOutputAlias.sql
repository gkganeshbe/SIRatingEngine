-- V011: OutputAlias and OperationScope on PipelineStep
--
-- OutputAlias: an alternative name under which the step's result is stored
-- in the risk bag (e.g. "FinalPremium"). Visible in the Admin UI step editor
-- so business users can give the result a meaningful name.
--
-- OperationScope: controls which part of the rating context the step operates on.
-- 'policy'   = runs once against the policy-level context (default for policy adjustments)
-- 'coverage' = runs once against the current coverage premium (default)
-- 'peril'    = runs for each peril separately

ALTER TABLE PipelineStep
    ADD OutputAlias    NVARCHAR(100) NULL,
        OperationScope VARCHAR(20)   NULL;
-- NULL means "use the engine default for the step's operation type"
