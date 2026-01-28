-- Tabela e registro inicial para rollout por % (Outbox Feature Flag)
-- Execute no banco da aplicação (ex.: SQL Server)

CREATE TABLE [dbo].[OutboxFeatureFlag](
    [Id] [int] IDENTITY(1,1) NOT NULL,
    [Ativo] [bit] NOT NULL DEFAULT 0,  -- 0=Desativado, 1=Ativo
    [PercentualVolume] [decimal](5,2) NOT NULL DEFAULT 0.00,  -- 0.00 a 100.00
    [DataInicio] [datetime2](7) NULL,
    [DataFim] [datetime2](7) NULL,
    [Observacao] [nvarchar](500) NULL,
    [DataUltimaAtualizacao] [datetime2](7) NOT NULL DEFAULT GETUTCDATE(),
    [UsuarioAtualizacao] [varchar](100) NULL,

    CONSTRAINT [PK_OutboxFeatureFlag] PRIMARY KEY CLUSTERED ([Id] ASC)
) ON [PRIMARY]
GO

INSERT INTO [dbo].[OutboxFeatureFlag] ([Ativo], [PercentualVolume], [Observacao])
VALUES (0, 0.00, 'Feature flag inicial - Outbox Pattern')
GO
