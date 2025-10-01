using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DentstageToolApp.Api.Customers;
using DentstageToolApp.Infrastructure.Data;
using DentstageToolApp.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DentstageToolApp.Api.Services.Customer;

/// <summary>
/// 客戶維運服務實作，負責處理新增客戶的資料整理、檢核與儲存流程。
/// </summary>
public class CustomerManagementService : ICustomerManagementService
{
    private readonly DentstageToolAppContext _dbContext;
    private readonly ILogger<CustomerManagementService> _logger;

    /// <summary>
    /// 建構子，注入資料庫內容物件與記錄器。
    /// </summary>
    public CustomerManagementService(DentstageToolAppContext dbContext, ILogger<CustomerManagementService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<CreateCustomerResponse> CreateCustomerAsync(CreateCustomerRequest request, string operatorName, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new CustomerManagementException(HttpStatusCode.BadRequest, "請提供客戶建立資料。");
        }

        // ---------- 參數整理區 ----------
        var customerName = NormalizeRequiredText(request.CustomerName, "客戶名稱");
        var operatorLabel = NormalizeOperator(operatorName);
        var customerType = NormalizeOptionalText(request.Category);
        var gender = NormalizeOptionalText(request.Gender);
        var county = NormalizeOptionalText(request.County);
        var township = NormalizeOptionalText(request.Township);
        var email = NormalizeEmail(request.Email);
        var source = NormalizeOptionalText(request.Source);
        var reason = NormalizeOptionalText(request.Reason);
        var remark = NormalizeOptionalText(request.Remark);
        var phone = NormalizeOptionalText(request.Phone);
        var phoneQuery = NormalizePhoneQuery(phone);

        cancellationToken.ThrowIfCancellationRequested();

        // ---------- 資料檢核區 ----------
        if (!string.IsNullOrEmpty(phoneQuery))
        {
            var duplicate = await _dbContext.Customers
                .AsNoTracking()
                .AnyAsync(customer =>
                    customer.PhoneQuery == phoneQuery
                    || customer.Phone == phone,
                    cancellationToken);

            if (duplicate)
            {
                throw new CustomerManagementException(HttpStatusCode.Conflict, "該聯絡電話已存在客戶資料，請勿重複新增。");
            }
        }

        // ---------- 實體建立區 ----------
        var now = DateTime.UtcNow;
        var customerEntity = new Customer
        {
            CustomerUid = BuildCustomerUid(),
            Name = customerName,
            CustomerType = customerType,
            Gender = gender,
            Phone = phone,
            PhoneQuery = phoneQuery,
            Email = email,
            County = county,
            Township = township,
            Source = source,
            Reason = reason,
            ConnectRemark = remark,
            CreationTimestamp = now,
            CreatedBy = operatorLabel,
            ModificationTimestamp = now,
            ModifiedBy = operatorLabel
        };

        await _dbContext.Customers.AddAsync(customerEntity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("操作人員 {Operator} 新增客戶 {CustomerUid} ({CustomerName}) 成功。", operatorLabel, customerEntity.CustomerUid, customerEntity.Name);

        // ---------- 組裝回應區 ----------
        return new CreateCustomerResponse
        {
            CustomerUid = customerEntity.CustomerUid,
            CustomerName = customerEntity.Name ?? customerName,
            Phone = customerEntity.Phone,
            Category = customerEntity.CustomerType,
            Gender = customerEntity.Gender,
            County = customerEntity.County,
            Township = customerEntity.Township,
            Email = customerEntity.Email,
            Source = customerEntity.Source,
            Reason = customerEntity.Reason,
            Remark = customerEntity.ConnectRemark,
            CreatedAt = now,
            Message = "已建立客戶資料。"
        };
    }

    // ---------- 方法區 ----------

    /// <summary>
    /// 產生符合規範的客戶主鍵，使用 Cu_ 前綴搭配 GUID。
    /// </summary>
    private static string BuildCustomerUid()
    {
        return $"Cu_{Guid.NewGuid().ToString().ToUpperInvariant()}";
    }

    /// <summary>
    /// 處理必填文字欄位，若為空值則拋出例外提示呼叫端補齊。
    /// </summary>
    private static string NormalizeRequiredText(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new CustomerManagementException(HttpStatusCode.BadRequest, $"{fieldName}為必填欄位，請重新輸入。");
        }

        return value.Trim();
    }

    /// <summary>
    /// 處理可選文字欄位，若為空白則回傳 null，避免寫入多餘空白。
    /// </summary>
    private static string? NormalizeOptionalText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    /// <summary>
    /// 將 Email 轉成統一格式，包含移除空白並轉為小寫。
    /// </summary>
    private static string? NormalizeEmail(string? email)
    {
        var normalized = NormalizeOptionalText(email);
        return normalized?.ToLowerInvariant();
    }

    /// <summary>
    /// 將電話欄位轉成僅包含數字的查詢字串，方便後續比對。
    /// </summary>
    private static string? NormalizePhoneQuery(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return null;
        }

        var digits = new string(phone.Where(char.IsDigit).ToArray());
        return string.IsNullOrWhiteSpace(digits) ? null : digits;
    }

    /// <summary>
    /// 將傳入的操作人員名稱正規化，避免寫入空白或 null。
    /// </summary>
    private static string NormalizeOperator(string? operatorName)
    {
        if (string.IsNullOrWhiteSpace(operatorName))
        {
            return "UnknownUser";
        }

        return operatorName.Trim();
    }

    // ---------- 生命週期 ----------
    // 服務為短期生命週期，由 DI 框架管理，無需額外生命週期邏輯。
}
