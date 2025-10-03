using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using PowNet.Extensions;
using PowNet.Common;

namespace PowNet.Common
{
    /// <summary>
    /// Base class for all domain entities with audit fields
    /// </summary>
    public abstract class BaseEntity : IEquatable<BaseEntity>, ICloneable
    {
        public virtual int Id { get; set; }
        public virtual DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public virtual DateTime? UpdatedAt { get; set; }
        public virtual string? CreatedBy { get; set; }
        public virtual string? UpdatedBy { get; set; }
        public virtual bool IsDeleted { get; set; } = false;
        public virtual DateTime? DeletedAt { get; set; }
        public virtual string? DeletedBy { get; set; }
        public virtual byte[] RowVersion { get; set; } = Array.Empty<byte>();

        /// <summary>
        /// Mark entity as deleted (soft delete)
        /// </summary>
        public virtual void MarkAsDeleted(string? deletedBy = null)
        {
            IsDeleted = true;
            DeletedAt = DateTime.UtcNow;
            DeletedBy = deletedBy;
        }

        /// <summary>
        /// Update audit fields
        /// </summary>
        public virtual void UpdateAuditFields(string? updatedBy = null)
        {
            UpdatedAt = DateTime.UtcNow;
            UpdatedBy = updatedBy;
        }

        public virtual bool Equals(BaseEntity? other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            if (GetType() != other.GetType()) return false;
            return Id == other.Id;
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as BaseEntity);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(GetType(), Id);
        }

        public virtual object Clone()
        {
            return MemberwiseClone();
        }

        public static bool operator ==(BaseEntity? left, BaseEntity? right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(BaseEntity? left, BaseEntity? right)
        {
            return !Equals(left, right);
        }
    }

    /// <summary>
    /// Base class for aggregate roots with domain events
    /// </summary>
    public abstract class AggregateRoot : BaseEntity
    {
        private readonly List<IDomainEvent> _domainEvents = new();

        [JsonIgnore]
        public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

        protected void AddDomainEvent(IDomainEvent domainEvent)
        {
            _domainEvents.Add(domainEvent);
        }

        public void RemoveDomainEvent(IDomainEvent domainEvent)
        {
            _domainEvents.Remove(domainEvent);
        }

        public void ClearDomainEvents()
        {
            _domainEvents.Clear();
        }
    }

    /// <summary>
    /// Base interface for domain events
    /// </summary>
    public interface IDomainEvent
    {
        DateTime OccurredOn { get; }
        string EventType { get; }
        object Data { get; }
    }

    /// <summary>
    /// Base implementation of domain events
    /// </summary>
    public abstract class DomainEvent : IDomainEvent
    {
        public DateTime OccurredOn { get; } = DateTime.UtcNow;
        public abstract string EventType { get; }
        public abstract object Data { get; }
    }

    /// <summary>
    /// Base class for value objects
    /// </summary>
    public abstract class ValueObject : IEquatable<ValueObject>
    {
        protected abstract IEnumerable<object?> GetEqualityComponents();

        public override bool Equals(object? obj)
        {
            if (obj == null || obj.GetType() != GetType())
                return false;

            var other = (ValueObject)obj;
            return GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());
        }

        public virtual bool Equals(ValueObject? other)
        {
            return Equals((object?)other);
        }

        public override int GetHashCode()
        {
            return GetEqualityComponents()
                .Select(x => x?.GetHashCode() ?? 0)
                .Aggregate((x, y) => x ^ y);
        }

        public static bool operator ==(ValueObject? left, ValueObject? right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(ValueObject? left, ValueObject? right)
        {
            return !Equals(left, right);
        }
    }

    /// <summary>
    /// Base class for API controllers with common functionality
    /// </summary>
    public abstract class BaseApiController
    {
        protected readonly Logging.Logger Logger;
        
        protected BaseApiController()
        {
            Logger = Logging.PowNetLogger.GetLogger(GetType().Name);
        }

        /// <summary>
        /// Create success response with data
        /// </summary>
        protected ApiResponse<T> Success<T>(T data, string? message = null)
        {
            return ApiResponse<T>.CreateSuccess(data, message);
        }

        /// <summary>
        /// Create success response without data
        /// </summary>
        protected ApiResponse Success(string? message = null)
        {
            return ApiResponse.CreateSuccess(message);
        }

        /// <summary>
        /// Create error response
        /// </summary>
        protected ApiResponse<T> Error<T>(string message, string? errorCode = null)
        {
            Logger.LogWarning("API Error: {Message} (Code: {ErrorCode})", message, errorCode);
            return ApiResponse<T>.CreateError(message, errorCode);
        }

        /// <summary>
        /// Create error response without data
        /// </summary>
        protected ApiResponse Error(string message, string? errorCode = null)
        {
            Logger.LogWarning("API Error: {Message} (Code: {ErrorCode})", message, errorCode);
            return ApiResponse.CreateError(message, errorCode);
        }

        /// <summary>
        /// Validate model and return validation errors if any
        /// </summary>
        protected ApiResponse? ValidateModel<T>(T model) where T : class
        {
            var validationResults = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
            var validationContext = new System.ComponentModel.DataAnnotations.ValidationContext(model);
            
            if (!System.ComponentModel.DataAnnotations.Validator.TryValidateObject(model, validationContext, validationResults, true))
            {
                var errors = validationResults.Select(vr => vr.ErrorMessage ?? "Validation error").ToList();
                return Error(string.Join("; ", errors), "VALIDATION_ERROR");
            }

            return null;
        }
    }

    /// <summary>
    /// Generic API response wrapper
    /// </summary>
    public class ApiResponse<T> : ApiResponse
    {
        public T? Data { get; set; }

        public static ApiResponse<T> CreateSuccess(T data, string? message = null)
        {
            return new ApiResponse<T>
            {
                IsSuccess = true,
                Message = message ?? "Operation completed successfully",
                Data = data,
                Timestamp = DateTime.UtcNow
            };
        }

        public static new ApiResponse<T> CreateError(string message, string? errorCode = null)
        {
            return new ApiResponse<T>
            {
                IsSuccess = false,
                Message = message,
                ErrorCode = errorCode,
                Timestamp = DateTime.UtcNow
            };
        }
    }

    /// <summary>
    /// Base API response class
    /// </summary>
    public class ApiResponse
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? ErrorCode { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public Dictionary<string, object> Metadata { get; set; } = new();

        public static ApiResponse CreateSuccess(string? message = null)
        {
            return new ApiResponse
            {
                IsSuccess = true,
                Message = message ?? "Operation completed successfully",
                Timestamp = DateTime.UtcNow
            };
        }

        public static ApiResponse CreateError(string message, string? errorCode = null)
        {
            return new ApiResponse
            {
                IsSuccess = false,
                Message = message,
                ErrorCode = errorCode,
                Timestamp = DateTime.UtcNow
            };
        }
    }

    /// <summary>
    /// Base class for repository pattern implementation
    /// </summary>
    public abstract class BaseRepository<TEntity> where TEntity : BaseEntity
    {
        protected readonly Logging.Logger Logger;

        protected BaseRepository()
        {
            Logger = Logging.PowNetLogger.GetLogger(GetType().Name);
        }

        public abstract Task<TEntity?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
        public abstract Task<IEnumerable<TEntity>> GetAllAsync(CancellationToken cancellationToken = default);
        public abstract Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default);
        public abstract Task<TEntity> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default);
        public abstract Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
        public abstract Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default);
        public abstract Task<int> CountAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Soft delete entity
        /// </summary>
        public virtual async Task<bool> SoftDeleteAsync(int id, string? deletedBy = null, CancellationToken cancellationToken = default)
        {
            var entity = await GetByIdAsync(id, cancellationToken);
            if (entity == null) return false;

            entity.MarkAsDeleted(deletedBy);
            await UpdateAsync(entity, cancellationToken);
            return true;
        }
    }

    /// <summary>
    /// Base class for service layer with common functionality
    /// </summary>
    public abstract class BaseService
    {
        protected readonly Logging.Logger Logger;
        
        protected BaseService()
        {
            Logger = Logging.PowNetLogger.GetLogger(GetType().Name);
        }

        /// <summary>
        /// Execute operation with logging and error handling
        /// </summary>
        protected async Task<TResult> ExecuteAsync<TResult>(
            Func<Task<TResult>> operation,
            string operationName,
            object? parameters = null)
        {
            Logger.LogDebug("Entering operation: {OperationName}", operationName);
            if (parameters != null)
            {
                Logger.LogDebug("Operation parameters: {@Parameters}", parameters);
            }
            
            try
            {
                using var measurement = Diagnostics.DiagnosticsManager.MeasurePerformance($"Service_{operationName}");
                var result = await operation();
                
                Logger.LogInformation("Service operation {OperationName} completed successfully", operationName);
                Logger.LogDebug("Operation result: {@Result}", result);
                
                return result;
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "Service operation {OperationName} failed", operationName);
                throw;
            }
        }

        /// <summary>
        /// Execute operation without return value
        /// </summary>
        protected async Task ExecuteAsync(
            Func<Task> operation,
            string operationName,
            object? parameters = null)
        {
            await ExecuteAsync(async () =>
            {
                await operation();
                return true;
            }, operationName, parameters);
        }
    }

    /// <summary>
    /// Specification pattern base class
    /// </summary>
    public abstract class Specification<T>
    {
        public abstract bool IsSatisfiedBy(T candidate);
        
        public Specification<T> And(Specification<T> other)
        {
            return new AndSpecification<T>(this, other);
        }

        public Specification<T> Or(Specification<T> other)
        {
            return new OrSpecification<T>(this, other);
        }

        public Specification<T> Not()
        {
            return new NotSpecification<T>(this);
        }
    }

    internal class AndSpecification<T> : Specification<T>
    {
        private readonly Specification<T> _left;
        private readonly Specification<T> _right;

        public AndSpecification(Specification<T> left, Specification<T> right)
        {
            _left = left;
            _right = right;
        }

        public override bool IsSatisfiedBy(T candidate)
        {
            return _left.IsSatisfiedBy(candidate) && _right.IsSatisfiedBy(candidate);
        }
    }

    internal class OrSpecification<T> : Specification<T>
    {
        private readonly Specification<T> _left;
        private readonly Specification<T> _right;

        public OrSpecification(Specification<T> left, Specification<T> right)
        {
            _left = left;
            _right = right;
        }

        public override bool IsSatisfiedBy(T candidate)
        {
            return _left.IsSatisfiedBy(candidate) || _right.IsSatisfiedBy(candidate);
        }
    }

    internal class NotSpecification<T> : Specification<T>
    {
        private readonly Specification<T> _spec;

        public NotSpecification(Specification<T> spec)
        {
            _spec = spec;
        }

        public override bool IsSatisfiedBy(T candidate)
        {
            return !_spec.IsSatisfiedBy(candidate);
        }
    }
}