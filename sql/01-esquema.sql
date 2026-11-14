SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.Libros', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Libros (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Libros PRIMARY KEY,
        Titulo nvarchar(160) NOT NULL,
        Autor nvarchar(120) NOT NULL,
        AnioPublicacion int NOT NULL CONSTRAINT CK_Libros_Anio CHECK (AnioPublicacion BETWEEN 1 AND 9999),
        Existencias int NOT NULL CONSTRAINT CK_Libros_Existencias CHECK (Existencias >= 0)
    );
END;

IF OBJECT_ID(N'dbo.Usuarios', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Usuarios (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Usuarios PRIMARY KEY,
        Correo nvarchar(254) NOT NULL,
        HashContrasena nvarchar(512) NOT NULL
    );
    CREATE UNIQUE INDEX IX_Usuarios_Correo ON dbo.Usuarios (Correo);
END;

COMMIT TRANSACTION;
