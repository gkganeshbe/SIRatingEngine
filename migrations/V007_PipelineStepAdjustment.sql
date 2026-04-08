-- V007: Add SourceType and ConstantValue to PipelineStep for the 'adjustment' step type
-- SourceType: 'rateTable' | 'constant' | 'stepOutput'
-- ConstantValue: the fixed value to apply when SourceType = 'constant'

ALTER TABLE PipelineStep
    ADD SourceType    VARCHAR(20)    NULL,
        ConstantValue DECIMAL(18,6)  NULL;
