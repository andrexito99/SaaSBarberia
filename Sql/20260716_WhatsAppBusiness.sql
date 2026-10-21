/* Módulo multi-tenant de WhatsApp Business (Meta Cloud API).
   Ejecutar manualmente en SQL Server antes de publicar el módulo. No usa migraciones EF. */

IF COL_LENGTH('dbo.Tenants', 'TieneModuloWhatsApp') IS NULL
BEGIN
    ALTER TABLE dbo.Tenants ADD TieneModuloWhatsApp bit NOT NULL
        CONSTRAINT DF_Tenants_TieneModuloWhatsApp DEFAULT (0);
END;
GO

IF OBJECT_ID('dbo.TenantModulos', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.TenantModulos (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_TenantModulos PRIMARY KEY,
        TenantId int NOT NULL,
        Codigo nvarchar(50) NOT NULL,
        Habilitado bit NOT NULL CONSTRAINT DF_TenantModulos_Habilitado DEFAULT (0),
        FechaActualizacion datetime2 NOT NULL CONSTRAINT DF_TenantModulos_FechaActualizacion DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT UQ_TenantModulos_Tenant_Codigo UNIQUE (TenantId, Codigo),
        CONSTRAINT FK_TenantModulos_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id) ON DELETE CASCADE
    );
END;
GO
INSERT INTO dbo.TenantModulos (TenantId, Codigo, Habilitado)
SELECT t.Id, v.Codigo, CASE WHEN v.Codigo = N'Herramientas' THEN ISNULL(t.TieneModuloHerramientas, 0) WHEN v.Codigo = N'WhatsApp' THEN ISNULL(t.TieneModuloWhatsApp, 0) ELSE 1 END
FROM dbo.Tenants t CROSS JOIN (VALUES (N'Agenda'),(N'Productos'),(N'Inventario'),(N'Herramientas'),(N'Caja'),(N'Reportes'),(N'Notificaciones'),(N'WhatsApp')) v(Codigo)
WHERE NOT EXISTS (SELECT 1 FROM dbo.TenantModulos m WHERE m.TenantId = t.Id AND m.Codigo = v.Codigo);
GO

CREATE TABLE dbo.WhatsAppConfiguraciones
(
    Id int IDENTITY(1,1) NOT NULL,
    TenantId int NOT NULL,
    NumeroWhatsApp nvarchar(30) NOT NULL,
    PhoneNumberId nvarchar(100) NOT NULL,
    BusinessAccountId nvarchar(100) NOT NULL,
    AccessTokenProtegido nvarchar(4000) NOT NULL,
    ConexionActiva bit NOT NULL CONSTRAINT DF_WhatsAppConfiguraciones_ConexionActiva DEFAULT (0),
    FechaConexion datetime2 NULL,
    EnviarConfirmaciones bit NOT NULL CONSTRAINT DF_WhatsAppConfiguraciones_Confirmaciones DEFAULT (1),
    EnviarRecordatorio24Horas bit NOT NULL CONSTRAINT DF_WhatsAppConfiguraciones_Recordatorio24 DEFAULT (1),
    EnviarRecordatorio2Horas bit NOT NULL CONSTRAINT DF_WhatsAppConfiguraciones_Recordatorio2 DEFAULT (1),
    EnviarCancelaciones bit NOT NULL CONSTRAINT DF_WhatsAppConfiguraciones_Cancelaciones DEFAULT (1),
    EnviarReprogramaciones bit NOT NULL CONSTRAINT DF_WhatsAppConfiguraciones_Reprogramaciones DEFAULT (1),
    PermitirMensajesPersonalizados bit NOT NULL CONSTRAINT DF_WhatsAppConfiguraciones_Personalizados DEFAULT (1),
    CONSTRAINT PK_WhatsAppConfiguraciones PRIMARY KEY (Id),
    CONSTRAINT UQ_WhatsAppConfiguraciones_Tenant UNIQUE (TenantId),
    CONSTRAINT FK_WhatsAppConfiguraciones_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id) ON DELETE CASCADE
);
GO

CREATE TABLE dbo.WhatsAppPlantillas
(
    Id int IDENTITY(1,1) NOT NULL,
    TenantId int NOT NULL,
    Tipo nvarchar(50) NOT NULL,
    Contenido nvarchar(4000) NOT NULL,
    Activa bit NOT NULL CONSTRAINT DF_WhatsAppPlantillas_Activa DEFAULT (1),
    FechaActualizacion datetime2 NOT NULL CONSTRAINT DF_WhatsAppPlantillas_FechaActualizacion DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_WhatsAppPlantillas PRIMARY KEY (Id),
    CONSTRAINT UQ_WhatsAppPlantillas_Tenant_Tipo UNIQUE (TenantId, Tipo),
    CONSTRAINT CK_WhatsAppPlantillas_Tipo CHECK (Tipo IN ('Confirmacion', 'Cancelacion', 'Reprogramacion', 'Recordatorio24Horas', 'Recordatorio2Horas', 'Bienvenida')),
    CONSTRAINT FK_WhatsAppPlantillas_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id) ON DELETE CASCADE
);
GO

CREATE TABLE dbo.WhatsAppMensajesHistorial
(
    Id int IDENTITY(1,1) NOT NULL,
    TenantId int NOT NULL,
    CitaId int NULL,
    ClienteId int NULL,
    Telefono nvarchar(30) NOT NULL,
    Tipo nvarchar(50) NOT NULL,
    Mensaje nvarchar(4000) NOT NULL,
    Fecha datetime2 NOT NULL CONSTRAINT DF_WhatsAppMensajesHistorial_Fecha DEFAULT (SYSUTCDATETIME()),
    Estado nvarchar(30) NOT NULL,
    Error nvarchar(2000) NULL,
    MetaMessageId nvarchar(200) NULL,
    CONSTRAINT PK_WhatsAppMensajesHistorial PRIMARY KEY (Id),
    CONSTRAINT CK_WhatsAppMensajesHistorial_Estado CHECK (Estado IN ('Pendiente', 'Enviado', 'Error')),
    CONSTRAINT FK_WhatsAppMensajesHistorial_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id) ON DELETE CASCADE,
    CONSTRAINT FK_WhatsAppMensajesHistorial_Citas FOREIGN KEY (CitaId) REFERENCES dbo.Citas(Id) ON DELETE SET NULL,
    CONSTRAINT FK_WhatsAppMensajesHistorial_Usuarios FOREIGN KEY (ClienteId) REFERENCES dbo.Usuarios(Id) ON DELETE SET NULL
);
GO

CREATE INDEX IX_WhatsAppMensajesHistorial_Cita_Tipo ON dbo.WhatsAppMensajesHistorial (TenantId, CitaId, Tipo);
CREATE INDEX IX_WhatsAppMensajesHistorial_Tenant_Fecha ON dbo.WhatsAppMensajesHistorial (TenantId, Fecha DESC);
GO

IF COL_LENGTH('dbo.WhatsAppConfiguraciones', 'EstadoConexion') IS NULL ALTER TABLE dbo.WhatsAppConfiguraciones ADD EstadoConexion nvarchar(20) NOT NULL CONSTRAINT DF_WhatsAppConfiguraciones_EstadoConexion DEFAULT (N'No conectado');
IF COL_LENGTH('dbo.WhatsAppConfiguraciones', 'WebhookVerifyTokenProtegido') IS NULL ALTER TABLE dbo.WhatsAppConfiguraciones ADD WebhookVerifyTokenProtegido nvarchar(4000) NULL;
IF COL_LENGTH('dbo.WhatsAppMensajesHistorial', 'BarberoId') IS NULL ALTER TABLE dbo.WhatsAppMensajesHistorial ADD BarberoId int NULL;
IF COL_LENGTH('dbo.WhatsAppMensajesHistorial', 'RespuestaMeta') IS NULL ALTER TABLE dbo.WhatsAppMensajesHistorial ADD RespuestaMeta nvarchar(4000) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_WhatsAppMensajesHistorial_Barberos') ALTER TABLE dbo.WhatsAppMensajesHistorial ADD CONSTRAINT FK_WhatsAppMensajesHistorial_Barberos FOREIGN KEY (BarberoId) REFERENCES dbo.Barberos(Id) ON DELETE SET NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_WhatsAppMensajesHistorial_Cita_Tipo') CREATE UNIQUE INDEX UX_WhatsAppMensajesHistorial_Cita_Tipo ON dbo.WhatsAppMensajesHistorial(TenantId, CitaId, Tipo) WHERE CitaId IS NOT NULL;
GO
