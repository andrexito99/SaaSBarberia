/* Ejecutar directamente en SQL Server. No usa migraciones de Entity Framework. */
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.BarberoAusencias', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.BarberoAusencias
    (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_BarberoAusencias PRIMARY KEY,
        TenantId INT NOT NULL,
        BarberoId INT NOT NULL,
        FechaInicio DATETIME2 NOT NULL,
        FechaFin DATETIME2 NOT NULL,
        Tipo INT NOT NULL, /* 0 Vacaciones, 1 Incapacidad, 2 Permiso, 3 Bloqueo Temporal, 4 Otro */
        Motivo NVARCHAR(250) NOT NULL,
        Observaciones NVARCHAR(1000) NULL,
        CreadoPorUsuarioId NVARCHAR(450) NOT NULL,
        Activo BIT NOT NULL CONSTRAINT DF_BarberoAusencias_Activo DEFAULT(1),
        FechaRegistro DATETIME2 NOT NULL CONSTRAINT DF_BarberoAusencias_FechaRegistro DEFAULT(GETDATE()),
        CONSTRAINT FK_BarberoAusencias_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id),
        CONSTRAINT FK_BarberoAusencias_Barberos FOREIGN KEY (BarberoId) REFERENCES dbo.Barberos(Id),
        CONSTRAINT CK_BarberoAusencias_Fechas CHECK (FechaFin > FechaInicio),
        CONSTRAINT CK_BarberoAusencias_Tipo CHECK (Tipo IN (0,1,2,3,4))
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_BarberoAusencias_Agenda' AND object_id = OBJECT_ID(N'dbo.BarberoAusencias'))
    CREATE INDEX IX_BarberoAusencias_Agenda ON dbo.BarberoAusencias(TenantId, BarberoId, FechaInicio, FechaFin);

/* Índices para los bloqueos de rango de la reserva serializable. */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Citas_Agenda_Tenant_Barbero_Fecha' AND object_id = OBJECT_ID(N'dbo.Citas'))
    CREATE INDEX IX_Citas_Agenda_Tenant_Barbero_Fecha ON dbo.Citas(TenantId, BarberoId, FechaHora);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Citas_Agenda_Tenant_Cliente_Fecha' AND object_id = OBJECT_ID(N'dbo.Citas'))
    CREATE INDEX IX_Citas_Agenda_Tenant_Cliente_Fecha ON dbo.Citas(TenantId, ClienteId, FechaHora);

IF OBJECT_ID(N'dbo.Notificaciones', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Notificaciones
    (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Notificaciones PRIMARY KEY,
        TenantId INT NOT NULL,
        UsuarioDestinoId INT NOT NULL,
        Titulo NVARCHAR(150) NOT NULL,
        Mensaje NVARCHAR(1000) NOT NULL,
        Tipo NVARCHAR(50) NOT NULL CONSTRAINT DF_Notificaciones_Tipo DEFAULT(N'Informacion'),
        Icono NVARCHAR(80) NULL,
        Color NVARCHAR(30) NULL,
        Leida BIT NOT NULL CONSTRAINT DF_Notificaciones_Leida DEFAULT(0),
        FechaCreacion DATETIME2 NOT NULL CONSTRAINT DF_Notificaciones_FechaCreacion DEFAULT(GETDATE()),
        FechaLectura DATETIME2 NULL,
        UrlDestino NVARCHAR(500) NULL,
        CONSTRAINT FK_Notificaciones_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id),
        CONSTRAINT FK_Notificaciones_Usuarios FOREIGN KEY (UsuarioDestinoId) REFERENCES dbo.Usuarios(Id)
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Notificaciones_Usuario_Bandeja' AND object_id = OBJECT_ID(N'dbo.Notificaciones'))
    CREATE INDEX IX_Notificaciones_Usuario_Bandeja ON dbo.Notificaciones(TenantId, UsuarioDestinoId, Leida, FechaCreacion DESC);

COMMIT TRANSACTION;
