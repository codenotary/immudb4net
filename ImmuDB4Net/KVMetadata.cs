/*
Copyright 2022 CodeNotary, Inc. All rights reserved.

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

	http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
*/

namespace ImmuDB;

/// <summary>
/// Represents the database key-value metadata
/// </summary>
public class KVMetadata
{

    internal static readonly byte deletedAttrCode = 0;
    internal static readonly byte expiresAtAttrCode = 1;
    internal static readonly byte nonIndexableAttrCode = 2;

    private static readonly int deletedAttrSize = 0;
    private static readonly int expiresAtAttrSize = 8;
    private static readonly int nonIndexableAttrSize = 0;

    private static readonly int maxKVMetadataLen = (MetadataAttribute.AttrCodeSize + deletedAttrSize) +
                                                (MetadataAttribute.AttrCodeSize + expiresAtAttrSize) +
                                                (MetadataAttribute.AttrCodeSize + nonIndexableAttrSize);

    private Dictionary<Byte, MetadataAttribute> attributes;

    /// <summary>
    /// Creates an empty KV metadata
    /// </summary>
    public KVMetadata()
    {
        attributes = new Dictionary<Byte, MetadataAttribute>();
    }

    /// <summary>
    /// Converts from a gRPC protobuf proxy KVMetadata object
    /// </summary>
    /// <param name="md"></param>
    /// <returns></returns>
    public static KVMetadata ValueOf(ImmudbProxy.KVMetadata md)
    {
        KVMetadata metadata = new KVMetadata();

        metadata.AsDeleted(md.Deleted);

        if (md.Expiration != null)
        {
            metadata.ExpiresAt(md.Expiration.ExpiresAt);
        }

        metadata.AsNonIndexable(md.NonIndexable);

        return metadata;
    }

    private void AsDeleted(bool deleted)
    {
        if (!deleted)
        {
            attributes.Remove(deletedAttrCode);
            return;
        }

        if (!attributes.ContainsKey(deletedAttrCode))
        {
            attributes[deletedAttrCode] = new DeletedAttribute();
        }

        return;
    }

    /// <summary>
    /// Is true if the deleted attribute is present
    /// </summary>
    public bool Deleted => attributes.ContainsKey(deletedAttrCode);

    private void AsNonIndexable(bool nonIndexable)
    {
        if (!nonIndexable)
        {
            attributes.Remove(nonIndexableAttrCode);
            return;
        }

        if (!attributes.ContainsKey(nonIndexableAttrCode))
        {
            attributes[nonIndexableAttrCode] = new NonIndexableAttribute();
        }

        return;
    }

    /// <summary>
    /// Is true if the non indexable attribute is set
    /// </summary>
    /// <value></value>
    public bool NonIndexable
    {
        get
        {
            return attributes.ContainsKey(nonIndexableAttrCode);
        }
    }

    private void ExpiresAt(long expirationTime)
    {
        ExpiresAtAttribute expiresAt;

        if (attributes.ContainsKey(expiresAtAttrCode))
        {
            expiresAt = (ExpiresAtAttribute)attributes[expiresAtAttrCode];
            expiresAt.ExpiresAt = expirationTime;
        }
        else
        {
            expiresAt = new ExpiresAtAttribute(expirationTime);
            attributes[expiresAtAttrCode] = expiresAt;
        }
    }

    /// <summary>
    /// Is true if the attributes contain expiration
    /// </summary>
    /// <value></value>
    public bool HasExpirationTime
    {
        get
        {
            return attributes.ContainsKey(expiresAtAttrCode);
        }
    }

    /// <summary>
    /// Gets the expiration time
    /// </summary>
    /// <value></value>
    public DateTime ExpirationTime
    {
        get
        {
            if (!attributes.ContainsKey(expiresAtAttrCode))
            {
                throw new InvalidOperationException("no expiration time set");
            }
            return DateTimeOffset.FromUnixTimeSeconds(((ExpiresAtAttribute)attributes[expiresAtAttrCode]).ExpiresAt).DateTime;
        }
    }

    /// <summary>
    /// Serializes to byte array
    /// </summary>
    /// <returns></returns>
    public byte[] Serialize()
    {
        MemoryStream bytes = new MemoryStream(maxKVMetadataLen);

        foreach (byte attrCode in new byte[] { deletedAttrCode, expiresAtAttrCode, nonIndexableAttrCode })
        {
            if (attributes.ContainsKey(attrCode))
            {
                bytes.WriteByte(attrCode);
                byte[] payload = attributes[attrCode].Serialize();
                bytes.Write(payload, 0, payload.Length);
            }
        }

        return bytes.ToArray();
    }
}

/// <summary>
/// Represents the deleted attribute
/// </summary>
public class DeletedAttribute : MetadataAttribute
{
    /// <summary>
    /// Gets the deleted attribute code
    /// </summary>
    /// <returns></returns>
    public override byte Code()
    {
        return KVMetadata.deletedAttrCode;
    }

    /// <summary>
    /// Serializes the attribute
    /// </summary>
    /// <returns></returns>
    public override byte[] Serialize()
    {
        return new byte[] { };
    }
}

/// <summary>
/// Represents 'the expires at' attribute
/// </summary>
public class ExpiresAtAttribute : MetadataAttribute
{
    /// <summary>
    /// Gets or Sets the expires at value
    /// </summary>
    /// <value></value>
    public long ExpiresAt { get; set; }
    
    /// <summary>
    /// Creates an instance of ExpiresAtAttribute
    /// </summary>
    /// <param name="expiresAt">The expiration time specified in epoch seconds</param>
    public ExpiresAtAttribute(long expiresAt)
    {
        this.ExpiresAt = expiresAt;
    }
    
    /// <summary>
    /// Creates an instance of ExpiresAtAttribute
    /// </summary>
    /// <param name="expiresAt">The expiration time</param>
    public ExpiresAtAttribute(DateTime expiresAt)
    {
        this.ExpiresAt = new DateTimeOffset(expiresAt).ToUnixTimeSeconds();
    }

    /// <summary>
    /// Gets the attribute code
    /// </summary>
    /// <returns></returns>
    public override byte Code()
    {
        return KVMetadata.expiresAtAttrCode;
    }

    /// <summary>
    /// Serializes the attribute to byte array
    /// </summary>
    /// <returns></returns>
    public override byte[] Serialize()
    {
        var bytes = BitConverter.GetBytes(ExpiresAt);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(bytes);
        }
        return bytes;
    }
}

/// <summary>
/// Represents the non-indexable attribute
/// </summary>
public class NonIndexableAttribute : MetadataAttribute
{
    /// <summary>
    /// Gets the attribute code
    /// </summary>
    /// <returns></returns>
    public override byte Code()
    {
        return KVMetadata.nonIndexableAttrCode;
    }

    /// <summary>
    /// Serializes the attribute to byte array
    /// </summary>
    /// <returns></returns>
    public override byte[] Serialize()
    {
        return new byte[] { };
    }
}