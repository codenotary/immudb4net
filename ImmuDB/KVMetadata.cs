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

    private KVMetadata()
    {
        attributes = new Dictionary<Byte, MetadataAttribute>();
    }

    public static KVMetadata ValueOf(ImmudbProxy.KVMetadata md)
    {
        KVMetadata metadata = new KVMetadata();

        metadata.AsDeleted(md.Deleted);

        if (md.Expiration != null)
        {
            metadata.expiresAt(md.Expiration.ExpiresAt);
        }

        metadata.asNonIndexable(md.NonIndexable);

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

    public bool Deleted()
    {
        return attributes.ContainsKey(deletedAttrCode);
    }

    private void asNonIndexable(bool nonIndexable)
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

    public bool nonIndexable()
    {
        return attributes.ContainsKey(nonIndexableAttrCode);
    }

    private void expiresAt(long expirationTime)
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

    public bool hasExpirationTime()
    {
        return attributes.ContainsKey(expiresAtAttrCode);
    }

    public long expirationTime()
    {
        if (!attributes.ContainsKey(expiresAtAttrCode))
        {
            throw new InvalidOperationException("no expiration time set");
        }

        return ((ExpiresAtAttribute)attributes[expiresAtAttrCode]).ExpiresAt;
    }

    public byte[] serialize()
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

public class DeletedAttribute : MetadataAttribute
{
    public override byte Code()
    {
        return KVMetadata.deletedAttrCode;
    }

    public override byte[] Serialize()
    {
        return new byte[] { };
    }
}

public class ExpiresAtAttribute : MetadataAttribute
{
    public long ExpiresAt { get; set; }
    public ExpiresAtAttribute(long expiresAt)
    {
        this.ExpiresAt = expiresAt;
    }

    public override byte Code()
    {
        return KVMetadata.expiresAtAttrCode;
    }

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

public class NonIndexableAttribute : MetadataAttribute
{
    public override byte Code()
    {
        return KVMetadata.nonIndexableAttrCode;
    }

    public override byte[] Serialize()
    {
        return new byte[] { };
    }
}