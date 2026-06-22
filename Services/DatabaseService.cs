using MySqlConnector;
using System.Threading;

namespace AzerothCoreManager.Services;

public class DatabaseService
{
    public async Task<bool> TestConnectionAsync(string host, int port, string user, string password, CancellationToken ct = default)
    {
        var connString = $"Server={host};Port={port};User ID={user};Password={password};AllowUserVariables=true";
        try
        {
            await using var conn = new MySqlConnection(connString);
            await conn.OpenAsync(ct);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<List<AccountInfo>> GetAccountsAsync(string host, int port, string user, string password, CancellationToken ct = default)
    {
        var connString = $"Server={host};Port={port};User ID={user};Password={password};Database=acore_auth;AllowUserVariables=true";
        var accounts = new List<AccountInfo>();

        await using var conn = new MySqlConnection(connString);
        await conn.OpenAsync(ct);

        var sql = @"SELECT a.id, a.username, a.email, a.joindate, a.last_ip, a.locked, 
                           COALESCE(aa.gmlevel, 0) as gmlevel
                    FROM account a
                    LEFT JOIN account_access aa ON a.id = aa.id AND aa.RealmID = -1
                    ORDER BY a.id";

        await using var cmd = new MySqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            accounts.Add(new AccountInfo
            {
                Id = reader.GetInt32("id"),
                Username = reader.GetString("username"),
                Email = reader.GetString("email"),
                JoinDate = reader.GetDateTime("joindate"),
                LastIp = reader.GetString("last_ip"),
                Locked = reader.GetBoolean("locked"),
                GmLevel = reader.GetInt32("gmlevel")
            });
        }

        return accounts;
    }

    public async Task<bool> CreateAccountAsync(string host, int port, string user, string password,
        string newUsername, string newPassword, int gmLevel = 0, CancellationToken ct = default)
    {
        var connString = $"Server={host};Port={port};User ID={user};Password={password};Database=acore_auth;AllowUserVariables=true";

        var salt = Srp6Helper.GenerateSalt();
        var verifier = Srp6Helper.ComputeVerifier(newUsername, newPassword, salt);

        await using var conn = new MySqlConnection(connString);
        await conn.OpenAsync(ct);

        var sql = @"INSERT INTO account (username, salt, verifier, email, reg_mail, joindate, expansion)
                    VALUES (@username, @salt, @verifier, '', '', NOW(), 2)";

        await using var cmd = new MySqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@username", newUsername.ToUpper());
        cmd.Parameters.AddWithValue("@salt", salt);
        cmd.Parameters.AddWithValue("@verifier", verifier);
        await cmd.ExecuteNonQueryAsync(ct);

        if (gmLevel > 0)
        {
            var gmSql = @"INSERT INTO account_access (id, gmlevel, RealmID)
                          SELECT id, @gmlevel, -1 FROM account WHERE username = @username
                          ON DUPLICATE KEY UPDATE gmlevel = @gmlevel";
            await using var gmCmd = new MySqlCommand(gmSql, conn);
            gmCmd.Parameters.AddWithValue("@username", newUsername.ToUpper());
            gmCmd.Parameters.AddWithValue("@gmlevel", gmLevel);
            await gmCmd.ExecuteNonQueryAsync(ct);
        }

        return true;
    }

    // === Character queries ===

    public async Task<List<CharacterInfo>> GetCharactersAsync(string host, int port, string user, string password, CancellationToken ct = default)
    {
        var connString = $"Server={host};Port={port};User ID={user};Password={password};Database=acore_characters;AllowUserVariables=true";
        var chars = new List<CharacterInfo>();

        await using var conn = new MySqlConnection(connString);
        await conn.OpenAsync(ct);

        var sql = @"SELECT c.guid, c.account, c.name, c.race, c.class, c.level, c.gender,
                           c.online, c.money, c.totaltime, c.logout_time
                    FROM characters c
                    ORDER BY c.name";

        await using var cmd = new MySqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            chars.Add(new CharacterInfo
            {
                Guid = reader.GetInt32("guid"),
                AccountId = reader.GetInt32("account"),
                Name = reader.GetString("name"),
                Race = reader.GetByte("race"),
                Class = reader.GetByte("class"),
                Level = reader.GetByte("level"),
                Gender = reader.GetByte("gender"),
                Online = reader.GetBoolean("online"),
                Money = reader.GetInt32("money"),
                TotalTime = reader.GetInt32("totaltime"),
                LogoutTime = reader.IsDBNull(reader.GetOrdinal("logout_time")) ? null : reader.GetDateTime("logout_time")
            });
        }

        return chars;
    }

    public async Task<List<CharacterInfo>> GetCharactersForAccountAsync(string host, int port, string user, string password, int accountId, CancellationToken ct = default)
    {
        var connString = $"Server={host};Port={port};User ID={user};Password={password};Database=acore_characters;AllowUserVariables=true";
        var chars = new List<CharacterInfo>();

        await using var conn = new MySqlConnection(connString);
        await conn.OpenAsync(ct);

        var sql = @"SELECT c.guid, c.account, c.name, c.race, c.class, c.level, c.gender,
                           c.online, c.money, c.totaltime, c.logout_time
                    FROM characters c
                    WHERE c.account = @accountId
                    ORDER BY c.name";

        await using var cmd = new MySqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@accountId", accountId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            chars.Add(new CharacterInfo
            {
                Guid = reader.GetInt32("guid"),
                AccountId = reader.GetInt32("account"),
                Name = reader.GetString("name"),
                Race = reader.GetByte("race"),
                Class = reader.GetByte("class"),
                Level = reader.GetByte("level"),
                Gender = reader.GetByte("gender"),
                Online = reader.GetBoolean("online"),
                Money = reader.GetInt32("money"),
                TotalTime = reader.GetInt32("totaltime"),
                LogoutTime = reader.IsDBNull(reader.GetOrdinal("logout_time")) ? null : reader.GetDateTime("logout_time")
            });
        }

        return chars;
    }

    // === Ban management ===

    public async Task<List<BanInfo>> GetAccountBansAsync(string host, int port, string user, string password, CancellationToken ct = default)
    {
        var connString = $"Server={host};Port={port};User ID={user};Password={password};Database=acore_auth;AllowUserVariables=true";
        var bans = new List<BanInfo>();

        await using var conn = new MySqlConnection(connString);
        await conn.OpenAsync(ct);

        var sql = @"SELECT ab.id, ab.bandate, ab.unbandate, ab.banreason, ab.active, a.username
                    FROM account_banned ab
                    JOIN account a ON ab.id = a.id
                    ORDER BY ab.bandate DESC";

        await using var cmd = new MySqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            bans.Add(new BanInfo
            {
                Id = reader.GetInt32("id"),
                AccountId = reader.GetInt32("id"),
                Username = reader.GetString("username"),
                BanDate = reader.GetDateTime("bandate"),
                UnbanDate = reader.GetDateTime("unbandate"),
                Reason = reader.GetString("banreason"),
                Active = reader.GetBoolean("active")
            });
        }

        return bans;
    }

    public async Task<bool> BanAccountAsync(string host, int port, string user, string password, int accountId, string reason, int days, CancellationToken ct = default)
    {
        var connString = $"Server={host};Port={port};User ID={user};Password={password};Database=acore_auth;AllowUserVariables=true";

        await using var conn = new MySqlConnection(connString);
        await conn.OpenAsync(ct);

        var sql = @"INSERT INTO account_banned (id, bandate, unbandate, banreason, bannedby, active)
                    VALUES (@id, NOW(), DATE_ADD(NOW(), INTERVAL @days DAY), @reason, 'AzerothCoreManager', 1)
                    ON DUPLICATE KEY UPDATE unbandate = DATE_ADD(NOW(), INTERVAL @days DAY), banreason = @reason, active = 1";

        await using var cmd = new MySqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@id", accountId);
        cmd.Parameters.AddWithValue("@days", days);
        cmd.Parameters.AddWithValue("@reason", reason);
        await cmd.ExecuteNonQueryAsync(ct);
        return true;
    }

    public async Task<bool> UnbanAccountAsync(string host, int port, string user, string password, int accountId, CancellationToken ct = default)
    {
        var connString = $"Server={host};Port={port};User ID={user};Password={password};Database=acore_auth;AllowUserVariables=true";

        await using var conn = new MySqlConnection(connString);
        await conn.OpenAsync(ct);

        var sql = @"UPDATE account_banned SET active = 0 WHERE id = @id AND active = 1";

        await using var cmd = new MySqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@id", accountId);
        await cmd.ExecuteNonQueryAsync(ct);
        return true;
    }

    // === Raw SQL query ===

    public async Task<(bool Success, List<string> Columns, List<List<string>> Rows, string? Error)> ExecuteQueryAsync(
        string host, int port, string user, string password, string database, string sql, CancellationToken ct = default)
    {
        var connString = $"Server={host};Port={port};User ID={user};Password={password};Database={database};AllowUserVariables=true";

        try
        {
            await using var conn = new MySqlConnection(connString);
            await conn.OpenAsync(ct);

            await using var cmd = new MySqlCommand(sql, conn);
            await using var reader = await cmd.ExecuteReaderAsync(ct);

            var columns = new List<string>();
            for (int i = 0; i < reader.FieldCount; i++)
                columns.Add(reader.GetName(i));

            var rows = new List<List<string>>();
            while (await reader.ReadAsync(ct))
            {
                var row = new List<string>();
                for (int i = 0; i < reader.FieldCount; i++)
                    row.Add(reader.IsDBNull(i) ? "NULL" : reader.GetValue(i)?.ToString() ?? "NULL");
                rows.Add(row);
            }

            return (true, columns, rows, null);
        }
        catch (Exception ex)
        {
            return (false, new(), new(), ex.Message);
        }
    }

    // === Data types ===

    public record CharacterInfo
    {
        public int Guid { get; init; }
        public int AccountId { get; init; }
        public string Name { get; init; } = "";
        public byte Race { get; init; }
        public byte Class { get; init; }
        public byte Level { get; init; }
        public byte Gender { get; init; }
        public bool Online { get; init; }
        public int Money { get; init; }
        public int TotalTime { get; init; }
        public DateTime? LogoutTime { get; init; }
    }

    public record BanInfo
    {
        public int Id { get; init; }
        public int AccountId { get; init; }
        public string Username { get; init; } = "";
        public DateTime BanDate { get; init; }
        public DateTime UnbanDate { get; init; }
        public string Reason { get; init; } = "";
        public bool Active { get; init; }
    }

    // === Player activity ===

    public async Task<List<PlayerActivityInfo>> GetPlayerActivityAsync(string host, int port, string user, string password, CancellationToken ct = default)
    {
        var connString = $"Server={host};Port={port};User ID={user};Password={password};Database=acore_auth;AllowUserVariables=true";
        var activity = new List<PlayerActivityInfo>();

        await using var conn = new MySqlConnection(connString);
        await conn.OpenAsync(ct);

        var sql = @"SELECT a.id, a.username, a.email, a.joindate, a.last_ip, a.locked,
                           COALESCE(aa.gmlevel, 0) as gmlevel,
                           COALESCE(chars.char_count, 0) as char_count,
                           COALESCE(chars.total_time, 0) as total_time,
                           COALESCE(chars.last_login, a.joindate) as last_login
                    FROM account a
                    LEFT JOIN account_access aa ON a.id = aa.id AND aa.RealmID = -1
                    LEFT JOIN (
                        SELECT account, COUNT(*) as char_count, SUM(totaltime) as total_time, MAX(logout_time) as last_login
                        FROM acore_characters.characters
                        GROUP BY account
                    ) chars ON a.id = chars.account
                    ORDER BY last_login DESC";

        await using var cmd = new MySqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            activity.Add(new PlayerActivityInfo
            {
                Id = reader.GetInt32("id"),
                Username = reader.GetString("username"),
                Email = reader.GetString("email"),
                JoinDate = reader.GetDateTime("joindate"),
                LastIp = reader.GetString("last_ip"),
                Locked = reader.GetBoolean("locked"),
                GmLevel = reader.GetInt32("gmlevel"),
                CharacterCount = reader.GetInt32("char_count"),
                TotalPlaytime = reader.GetInt32("total_time"),
                LastLogin = reader.GetDateTime("last_login")
            });
        }

        return activity;
    }

    // === GM audit log ===

    public async Task<List<GmAuditEntry>> GetGmAuditLogAsync(string host, int port, string user, string password, CancellationToken ct = default)
    {
        var connString = $"Server={host};Port={port};User ID={user};Password={password};Database=acore_characters;AllowUserVariables=true";
        var entries = new List<GmAuditEntry>();

        await using var conn = new MySqlConnection(connString);
        await conn.OpenAsync(ct);

        // gm_ticket table tracks GM actions
        var sql = @"SELECT gt.guid, gt.type, gt.createTime, gt.lastModifiedTime, gt.closedBy,
                           COALESCE(a.username, 'System') as gm_name
                    FROM gm_ticket gt
                    LEFT JOIN acore_auth.account a ON gt.closedBy = a.id
                    ORDER BY gt.lastModifiedTime DESC
                    LIMIT 200";

        await using var cmd = new MySqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            entries.Add(new GmAuditEntry
            {
                Id = reader.GetInt32("guid"),
                Type = reader.GetInt32("type").ToString(),
                Time = reader.GetDateTime("lastModifiedTime"),
                GmUsername = reader.GetString("gm_name"),
                RealmId = 0,
                Command = $"Ticket #{reader.GetInt32("guid")} closed (type={reader.GetInt32("type")})"
            });
        }

        return entries;
    }

    // === Data types ===

    public record PlayerActivityInfo
    {
        public int Id { get; init; }
        public string Username { get; init; } = "";
        public string Email { get; init; } = "";
        public DateTime JoinDate { get; init; }
        public string LastIp { get; init; } = "";
        public bool Locked { get; init; }
        public int GmLevel { get; init; }
        public int CharacterCount { get; init; }
        public int TotalPlaytime { get; init; }
        public DateTime LastLogin { get; init; }
    }

    public record GmAuditEntry
    {
        public int Id { get; init; }
        public DateTime Time { get; init; }
        public string GmUsername { get; init; } = "";
        public int RealmId { get; init; }
        public string Type { get; init; } = "";
        public string Command { get; init; } = "";
    }

    public record AccountInfo
    {
        public int Id { get; init; }
        public string Username { get; init; } = "";
        public string Email { get; init; } = "";
        public DateTime JoinDate { get; init; }
        public string LastIp { get; init; } = "";
        public bool Locked { get; init; }
        public int GmLevel { get; init; }
    }
}
