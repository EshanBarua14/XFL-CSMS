/*
  Demo data for the first stable version (v1-original, scripts/run-original.sh, http://localhost:5201, DB CSMS_Original).
  Run with scripts/seed-original.sh, or open in SSMS and Execute against CSMS_Original.

  Only what the v1 schema has: roles Admin / Support Manager / Support Engineer / Maker (no viewer, no levels,
  no ratings or notifications). Safe to run again: accounts are added only when the user name is missing,
  the tickets only once (they are marked with Comments = 'Sample data (demo seed)').
  Every demo account's password is Demo@12345 (the first admin keeps its own password).
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @hash varbinary(max) = 0x68DE4DEFE19A6B1AEEA69838D5CDCF5723E0130D8F28CF15BF61E6861B20B29E2731A273D963B2E8A4A1EDCDE3A0E2062B204BDA96D6183C2E181C5724706E0B;
DECLARE @salt varbinary(max) = 0x87B8A0DDAD2F6E68C8D9EC99FC06795E35D8A437D1696574CFB621607D8227D96FF88435D6EFBCD417BDDD0F0B581FDB480D60CB14D0F4D7E4616F5FFC584121;
DECLARE @now datetime2 = SYSDATETIME();

-- ---- houses and branches ---------------------------------------------------------------------------
DECLARE @xfl int = (SELECT TOP 1 BrokerageId FROM Brokerages WHERE BrokerageHouseAcronym = 'XFL' ORDER BY BrokerageId);
IF @xfl IS NULL SET @xfl = (SELECT TOP 1 BrokerageId FROM Brokerages ORDER BY BrokerageId);
IF @xfl IS NULL
BEGIN
    INSERT INTO Brokerages (BrokerageHouseName, BrokerageHouseAcronym) VALUES ('Xpert Fintech Limited', 'XFL');
    SET @xfl = SCOPE_IDENTITY();
END
IF NOT EXISTS (SELECT 1 FROM Brokerages WHERE BrokerageHouseAcronym = 'ASL')
    INSERT INTO Brokerages (BrokerageHouseName, BrokerageHouseAcronym) VALUES ('Alpha Securities Limited', 'ASL');
IF NOT EXISTS (SELECT 1 FROM Brokerages WHERE BrokerageHouseAcronym = 'BCL')
    INSERT INTO Brokerages (BrokerageHouseName, BrokerageHouseAcronym) VALUES ('Beta Capital Limited', 'BCL');
DECLARE @asl int = (SELECT TOP 1 BrokerageId FROM Brokerages WHERE BrokerageHouseAcronym = 'ASL' ORDER BY BrokerageId);
DECLARE @bcl int = (SELECT TOP 1 BrokerageId FROM Brokerages WHERE BrokerageHouseAcronym = 'BCL' ORDER BY BrokerageId);

IF NOT EXISTS (SELECT 1 FROM Branchhs WHERE BrokerageId = @xfl) INSERT INTO Branchhs (BranchName, BrokerageId) VALUES ('Head Office', @xfl);
IF NOT EXISTS (SELECT 1 FROM Branchhs WHERE BrokerageId = @asl AND BranchName = 'Motijheel') INSERT INTO Branchhs (BranchName, BrokerageId) VALUES ('Motijheel', @asl);
IF NOT EXISTS (SELECT 1 FROM Branchhs WHERE BrokerageId = @asl AND BranchName = 'Chattogram') INSERT INTO Branchhs (BranchName, BrokerageId) VALUES ('Chattogram', @asl);
IF NOT EXISTS (SELECT 1 FROM Branchhs WHERE BrokerageId = @bcl AND BranchName = 'Gulshan') INSERT INTO Branchhs (BranchName, BrokerageId) VALUES ('Gulshan', @bcl);
DECLARE @xflHead int = (SELECT TOP 1 BranchId FROM Branchhs WHERE BrokerageId = @xfl ORDER BY BranchId);
DECLARE @aslHead int = (SELECT TOP 1 BranchId FROM Branchhs WHERE BrokerageId = @asl AND BranchName = 'Motijheel');
DECLARE @aslCtg int = (SELECT TOP 1 BranchId FROM Branchhs WHERE BrokerageId = @asl AND BranchName = 'Chattogram');
DECLARE @bclHead int = (SELECT TOP 1 BranchId FROM Branchhs WHERE BrokerageId = @bcl AND BranchName = 'Gulshan');

-- ---- support lookups ---------------------------------------------------------------------------------
INSERT INTO SupportTypes (SType) SELECT v FROM (VALUES ('Software'), ('Hardware'), ('Network')) x(v) WHERE NOT EXISTS (SELECT 1 FROM SupportTypes WHERE SType = x.v);
INSERT INTO SupportCatagories (SCatagory) SELECT v FROM (VALUES ('Trading'), ('Reporting'), ('Accounts')) x(v) WHERE NOT EXISTS (SELECT 1 FROM SupportCatagories WHERE SCatagory = x.v);
INSERT INTO SupportSubCatagories (SubCatagory) SELECT v FROM (VALUES ('OrderEntry'), ('Settlement'), ('Portfolio')) x(v) WHERE NOT EXISTS (SELECT 1 FROM SupportSubCatagories WHERE SubCatagory = x.v);
INSERT INTO AffectedSectionss (ASection) SELECT v FROM (VALUES ('Back Office'), ('Trading Terminal'), ('Client Portal')) x(v) WHERE NOT EXISTS (SELECT 1 FROM AffectedSectionss WHERE ASection = x.v);

-- ---- accounts (the way Admin > Create User stores them) -----------------------------------------------
DECLARE @acc TABLE (UserName nvarchar(100), FullName nvarchar(200), Department nvarchar(100), Designation nvarchar(100),
                    BranchId int, IsAdmin bit, IsStaff bit, Active bit);
INSERT INTO @acc VALUES
    ('admin2',         'Second Administrator',   'Maker',            'Administrator',    @xflHead, 1, 1, 1),
    ('manager',        'Support Manager',        'Support Maneger',  'Support Manager',  @xflHead, 0, 1, 1),
    ('manager2',       'Nadia Rahman',           'Support Maneger',  'Support Manager',  @xflHead, 0, 1, 1),
    ('engineer1',      'Support Engineer One',   'Support Engineer', 'Support Engineer', @xflHead, 0, 1, 1),
    ('engineer2',      'Support Engineer Two',   'Support Engineer', 'Support Engineer', @xflHead, 0, 1, 1),
    ('engineer3',      'Support Engineer Three', 'Support Engineer', 'Support Engineer', @xflHead, 0, 1, 1),
    ('engineer4',      'Support Engineer Four',  'Support Engineer', 'Support Engineer', @xflHead, 0, 1, 1),
    ('engineer5',      'Support Engineer Five',  'Support Engineer', 'Support Engineer', @xflHead, 0, 1, 1),
    ('maker.alpha',    'Alpha Maker',            'Maker',            'Maker',            @aslHead, 0, 0, 1),
    ('maker.alpha2',   'Alpha Branch Maker',     'Maker',            'Maker',            @aslCtg,  0, 0, 1),
    ('maker.beta',     'Beta Maker',             'Maker',            'Maker',            @bclHead, 0, 0, 1),
    ('maker.beta2',    'Beta Second Maker',      'Maker',            'Maker',            @bclHead, 0, 0, 1),
    ('maker.inactive', 'Inactive Maker',         'Maker',            'Maker',            @bclHead, 0, 0, 0);

INSERT INTO Users (FullName, Email, PhonNumber, Designation, Department, EmployeeId, UserName, PasswordHash, PasswordSalt,
                   VerifiedAt, BrokerageHouseName, BrokerageHouseAcronym, Branch, UCatagory, UType, UStatus, Terms)
SELECT a.FullName, a.UserName + '@example.com', '01700000000', a.Designation, a.Department, 'D-' + a.UserName, a.UserName,
       @hash, @salt, @now, b.BrokerageId, b.BrokerageId, a.BranchId, a.IsAdmin, a.IsStaff, a.Active, 1
FROM @acc a JOIN Branchhs b ON b.BranchId = a.BranchId
WHERE NOT EXISTS (SELECT 1 FROM Users u WHERE u.UserName = a.UserName)
  AND NOT EXISTS (SELECT 1 FROM Users u WHERE u.Email = a.UserName + '@example.com');
PRINT CONCAT('Accounts added: ', @@ROWCOUNT);

-- ---- tickets in every status (once) ---------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM Issues WHERE Comments = 'Sample data (demo seed)')
BEGIN
    DECLARE @t TABLE (Seq int IDENTITY, Owner nvarchar(100), House int, Title nvarchar(200), Priority nvarchar(20),
                      DaysAgo int, Status nvarchar(20), Engineer nvarchar(100), ClosedDaysAgo int NULL);
    INSERT INTO @t (Owner, House, Title, Priority, DaysAgo, Status, Engineer, ClosedDaysAgo) VALUES
        -- unassigned (Open)
        ('maker.alpha',  @asl, 'Order entry screen freezes',          'High',   0, 'Open',       NULL,        NULL),
        ('maker.alpha2', @asl, 'Branch report shows no data',         'Medium', 1, 'Open',       NULL,        NULL),
        ('maker.beta',   @bcl, 'Client portal login slow',            'Low',    2, 'Open',       NULL,        NULL),
        ('maker.beta2',  @bcl, 'Margin call alert not sent',          'High',   6, 'Open',       NULL,        NULL),
        -- assigned, waiting / in work
        ('maker.alpha2', @asl, 'Portfolio value mismatch',            'Medium', 8, 'Open',       'engineer1', NULL),
        ('maker.beta2',  @bcl, 'Printer not working',                 'Low',    5, 'Inqueue',    'engineer2', NULL),
        ('maker.beta',   @bcl, 'Cannot export client ledger',         'Low',    4, 'Inqueue',    'engineer3', NULL),
        ('maker.alpha',  @asl, 'Settlement file rejected',            'High',   3, 'Inprogress', 'engineer1', NULL),
        ('maker.beta',   @bcl, 'Day-end process takes 3 hours',       'High',   9, 'Inprogress', 'engineer4', NULL),
        ('maker.alpha2', @asl, 'Commission calculation for odd lots', 'Medium', 7, 'Inprogress', 'engineer5', NULL),
        ('manager',      @asl, 'Phone request: add new user',         'Low',    4, 'Inprogress', 'engineer2', NULL),
        -- closed
        ('maker.alpha',  @asl, 'Password reset for trader',           'Medium', 12, 'Close',     'engineer1', 12),
        ('maker.beta',   @bcl, 'VPN disconnects',                     'High',   20, 'Close',     'engineer2', 19),
        ('maker.beta2',  @bcl, 'Tax certificate shows wrong year',    'Medium', 30, 'Close',     'engineer4', 25),
        ('maker.alpha2', @asl, 'User locked out of back office',      'Low',    35, 'Close',     'engineer3', 35),
        ('maker.beta',   @bcl, 'Day end report missing',              'Medium', 40, 'Close',     'engineer5', 38),
        ('maker.alpha',  @asl, 'Change printer for contract notes',   'Low',     2, 'Close',     'engineer1', 1);

    DECLARE @mgr nvarchar(200) = (SELECT FullName FROM Users WHERE UserName = 'manager');
    DECLARE @seq int = 1, @max int = (SELECT MAX(Seq) FROM @t);
    WHILE @seq <= @max
    BEGIN
        DECLARE @owner int, @ownerName nvarchar(200), @house int, @acr nvarchar(50), @title nvarchar(200), @prio nvarchar(20),
                @days int, @status nvarchar(20), @eng nvarchar(200), @closed int, @num int, @tdate datetime2;
        SELECT @owner = u.Id, @ownerName = u.FullName, @house = t.House, @title = t.Title, @prio = t.Priority, @days = t.DaysAgo,
               @status = t.Status, @eng = e.FullName, @closed = t.ClosedDaysAgo
        FROM @t t JOIN Users u ON u.UserName = t.Owner LEFT JOIN Users e ON e.UserName = t.Engineer
        WHERE t.Seq = @seq;
        SET @acr = (SELECT BrokerageHouseAcronym FROM Brokerages WHERE BrokerageId = @house);
        SET @num = ISNULL((SELECT MAX(TRY_CAST(SUBSTRING(TNumber, LEN(@acr) + 2, 20) AS int)) FROM Issues WHERE TNumber LIKE @acr + '[_]%'), 0) + 1;
        SET @tdate = DATEADD(HOUR, -2 - @seq, DATEADD(DAY, -@days, @now));

        INSERT INTO Issues (UserId, BrokerageId, TDate, TNumber, ITitle, Priority, Details, Comments,
                            SupportTypeId, SupportCatagoryId, SupportSubCatagoryId, AffectedSectionId,
                            IStatus, AssignBy, AssignOn, ApproveBy, ApproveOn, UpdatedBy, UpdatedOn, ClosedBy, ClosedOn)
        VALUES (@owner, @house, @tdate, @acr + '_' + RIGHT('0000000' + CAST(@num AS varchar(7)), 7), @title, @prio,
                '<p>' + @title + ' (sample ticket).</p>', 'Sample data (demo seed)',
                (SELECT TOP 1 SupportTypeId FROM SupportTypes ORDER BY SupportTypeId),
                (SELECT TOP 1 SupportCatagoryId FROM SupportCatagories ORDER BY SupportCatagoryId),
                (SELECT TOP 1 SupportSubCatagoryId FROM SupportSubCatagories ORDER BY SupportSubCatagoryId),
                (SELECT TOP 1 AffectedSectionId FROM AffectedSectionss ORDER BY AffectedSectionId),
                @status,
                @eng,
                CASE WHEN @eng IS NULL THEN NULL ELSE DATEADD(HOUR, 1, @tdate) END,
                CASE WHEN @eng IS NULL THEN NULL ELSE @mgr END,
                CASE WHEN @eng IS NULL THEN NULL ELSE DATEADD(HOUR, 1, @tdate) END,
                CASE WHEN @eng IS NULL THEN NULL ELSE @eng END,
                CASE WHEN @eng IS NULL THEN NULL ELSE DATEADD(HOUR, 2, @tdate) END,
                CASE WHEN @status = 'Close' THEN @eng ELSE NULL END,
                CASE WHEN @status = 'Close' THEN DATEADD(HOUR, 6, DATEADD(DAY, -@closed, @now)) ELSE NULL END);
        SET @seq += 1;
    END

    -- to-dos
    INSERT INTO Todos (Todoname, Status, CreatedOn, UserId, BrokerageId)
    SELECT n.Todoname, n.Status, DATEADD(DAY, -n.DaysAgo, @now), u.Id, u.BrokerageHouseName
    FROM Users u CROSS JOIN (VALUES ('Check open tickets', 'In progress', 0), ('Weekly summary', 'Done', 3), ('Call the vendor', 'Canceled', 5)) n(Todoname, Status, DaysAgo)
    WHERE u.UserName IN ('manager', 'engineer1', 'engineer2', 'maker.alpha', 'maker.beta');

    PRINT CONCAT('Tickets added: ', @max);
END
ELSE
    PRINT 'Demo tickets already there: skipped.';

COMMIT;
