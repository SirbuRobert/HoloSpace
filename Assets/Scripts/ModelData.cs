using System;
using UnityEngine;

/// <summary>
/// DTO-uri pentru lista si metadatele modelelor de pe server.
/// Serializabile cu JsonUtility (fara Dictionary, se folosesc array-uri).
///
/// Format JSON server:
///   Index:    [{"id":"...","title":"...","uploadedAt":"..."}]
///   Metadata: {"id":"...","title":"...","uploadedAt":"...","parts":[{"name":"...","description":"..."}]}
/// </summary>

[Serializable]
public class ModelIndexEntry
{
    public string id;
    public string title;
    public string uploadedAt;
}

/// <summary>Wrapper pentru a deserializa array-ul JSON cu JsonUtility.</summary>
[Serializable]
public class ModelIndexWrapper
{
    public ModelIndexEntry[] models;
}

[Serializable]
public class ModelPart
{
    public string name;
    public string description;
}

[Serializable]
public class ModelMetadata
{
    public string      id;
    public string      title;
    public string      uploadedAt;
    public ModelPart[] parts;

    /// <summary>Cauta descrierea unui nod dupa nume (case-insensitive).</summary>
    public string GetDescription(string nodeName)
    {
        if (parts == null) return string.Empty;
        foreach (var p in parts)
            if (string.Equals(p.name, nodeName, StringComparison.OrdinalIgnoreCase))
                return p.description ?? string.Empty;
        return string.Empty;
    }
}
