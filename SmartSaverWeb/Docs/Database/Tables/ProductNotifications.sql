CREATE TABLE dbo.ProductNotifications (
    NotificationId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
    ProductId UNIQUEIDENTIFIER NOT NULL,         -- FK to TrackedProducts
    UserId UNIQUEIDENTIFIER NOT NULL,            -- redundant but helpful for fast queries
    RuleType NVARCHAR(50) NOT NULL,              -- percent-drop, fixed, all-time-low, etc.
    DropType NVARCHAR(50) NOT NULL,              -- percent, amount
    DropValue DECIMAL(18,2) NOT NULL,            -- 10 (%), 5 ($), etc.
    NotificationValue DECIMAL(18,2) NOT NULL,    -- computed threshold, e.g. < $9.00
    SavedPrice DECIMAL(18,2) NOT NULL,           -- baseline price at time of rule creation
    DateSet DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    DateModified DATETIME2 NULL,
    IsActive BIT NOT NULL DEFAULT 1,

    CONSTRAINT PK_ProductNotifications PRIMARY KEY (NotificationId),
    CONSTRAINT FK_ProductNotifications_TrackedProducts FOREIGN KEY (ProductId)
        REFERENCES dbo.TrackedProducts(ProductId),
    CONSTRAINT FK_ProductNotifications_Users FOREIGN KEY (UserId)
        REFERENCES dbo.Users(UserId)
);
