# peakmap.top Public API

This document describes the public HTTP API used by PEAK Map Browser.

Live API verification: 2026-07-13.

## Base URL

Production:

```text
https://peakmap.top
```

Local development deployments use the same paths below, for example:

```text
http://localhost:3000
```

The public read and download endpoints do not require an API key. Uploads are rate-limited and validated server-side.

## General Conventions

- JSON responses use `Content-Type: application/json; charset=utf-8` unless the endpoint returns a file.
- Field names are always English.
- Map names, authors, MOD version strings, and descriptions are returned as stored. They are not translated.
- `created_at` values are ISO 8601 timestamps.
- `lang=zh` or `lang=en` only changes localized error text for the public list endpoints.
- Unknown `lang` values fall back to `zh`.
- The API returns absolute URLs for `image_url`, `thumbnail_url`, `json_file_url`, and `download_url`.
- Clients should consume returned URLs directly and should not construct Supabase or R2 URLs themselves.

## Endpoint Summary

| Method | Path | Purpose |
| --- | --- | --- |
| GET | `/api/maps` | Query the public map list |
| GET | `/api/mod-versions` | Query available MOD versions |
| POST | `/api/upload` | Upload a map JSON file and optional cover image |
| GET | `/api/download/{id}` | Download a map JSON file by map ID |
| GET | `/api/r2/{key}` | Public file proxy for returned image/JSON resource URLs |

## GET /api/maps

Returns a paginated list of public maps.

### Query Parameters

| Parameter | Type | Default | Rules |
| --- | --- | --- | --- |
| `page` | integer | `1` | Minimum `1`; invalid values use the default; values are clamped to `1..999999` |
| `page_size` | integer | `12` | Minimum `1`, maximum `50`; invalid values use the default; values are clamped to `1..50` |
| `q` | string | empty | Case-insensitive search across `name`, `author`, and `mod_version` |
| `sort` | string | `newest` | `newest` or `downloads`; unknown values use `newest` |
| `mod_version` | string | empty | Exact match against `mod_version` |
| `lang` | string | `zh` | `zh` or `en`; only affects `error_message` |

### Sorting

- `newest`: descending `created_at` order.
- `downloads`: descending `downloads` order.

### Pagination Behavior

The response always includes the total count. If the requested page is beyond the last page, the API returns HTTP `200` with an empty `data` array and a valid `pagination` object.

### Example Request

```http
GET /api/maps?q=mountain&sort=downloads&page=1&page_size=20&lang=en HTTP/1.1
Host: peakmap.top
Accept: application/json
```

### Success Response

```json
{
  "success": true,
  "data": [
    {
      "id": "c75a357d-da96-4ed4-8ebe-30f60a99e683",
      "name": "Don't Cook Dynos",
      "author": "Hobbs0406",
      "mod_version": "0.3.2(CN_0.1.2)",
      "description": "Desc: ...",
      "downloads": 59,
      "created_at": "2026-05-29T06:03:15.333142+00:00",
      "image_url": "https://peakmap.top/api/r2/images/example.png",
      "thumbnail_url": "https://peakmap.top/api/r2/images/example.png",
      "json_file_url": "https://peakmap.top/api/r2/maps/example.json",
      "download_url": "https://peakmap.top/api/download/c75a357d-da96-4ed4-8ebe-30f60a99e683"
    }
  ],
  "pagination": {
    "page": 1,
    "page_size": 20,
    "total": 1,
    "total_pages": 1,
    "has_next": false,
    "has_prev": false
  }
}
```

### Map Object

| Field | Type | Description |
| --- | --- | --- |
| `id` | string | UUID map identifier |
| `name` | string | Map display name |
| `author` | string | Stored author name |
| `mod_version` | string | Exact MOD version string associated with the map |
| `description` | string | Original map description; empty string when absent |
| `downloads` | integer | Download counter |
| `created_at` | string | ISO 8601 creation timestamp |
| `image_url` | string | Absolute cover image URL; may be empty |
| `thumbnail_url` | string | Absolute thumbnail URL; currently uses the same stored image URL and may be empty |
| `json_file_url` | string | Absolute direct JSON resource URL; may be empty only if the stored record is incomplete |
| `download_url` | string | Recommended download endpoint for the map |

### Error Response

Database or server failures use this structure:

```json
{
  "success": false,
  "error_code": "FETCH_MAPS_FAILED",
  "error": "Failed to fetch maps",
  "error_message": "Failed to fetch maps"
}
```

`error_code` is stable for machine handling. `error` is an English server/log message. `error_message` is localized using `lang`.

## GET /api/mod-versions

Returns the available MOD version list, newest first.

### Query Parameters

| Parameter | Type | Default | Description |
| --- | --- | --- | --- |
| `lang` | string | `zh` | `zh` or `en`; only affects `error_message` |

### Example Request

```http
GET /api/mod-versions?lang=en HTTP/1.1
Host: peakmap.top
Accept: application/json
```

### Success Response

```json
{
  "success": true,
  "data": [
    {
      "id": "bdf79284-3c13-4467-88ae-81b664c94099",
      "version_name": "0.3.2(CN_0.1.2)",
      "created_at": "2026-05-29T00:52:24.179511+00:00"
    }
  ]
}
```

### Error Code

`FETCH_MOD_VERSIONS_FAILED`

## POST /api/upload

Uploads a TerrainCustomiser map JSON file and an optional cover image.

This endpoint requires `multipart/form-data`. Do not send JSON request bodies.

### Form Fields

| Field | Type | Required | Rules |
| --- | --- | --- | --- |
| `name` | text | yes | Maximum 100 characters |
| `author` | text | yes | Maximum 50 characters |
| `mod_version` | text | yes | Maximum 20 characters |
| `description` | text | no | Maximum 2000 characters; defaults to empty |
| `json_file` | file | yes | `.json`, `application/json` or `text/plain`, maximum 2 MB by default |
| `image_file` | file | no | `.jpg`, `.jpeg`, `.png`, `.gif`, or `.webp`; maximum 1 MB by default |

SVG images are rejected. Metadata containing common script/XSS patterns is rejected.

The size limits are read from server configuration and may be changed by the site administrator. The documented defaults are 2 MB for JSON and 1 MB for images.

### Example with curl

```bash
curl -X POST "https://peakmap.top/api/upload" \
  -F "name=My Map" \
  -F "author=SteamName" \
  -F "mod_version=0.3.2(CN_0.1.2)" \
  -F "description=An optional map description" \
  -F "json_file=@./my-map.json;type=application/json" \
  -F "image_file=@./my-map.png;type=image/png"
```

### Success Response

```json
{
  "success": true,
  "message": "地图上传成功"
}
```

The current server message is Chinese even when the client UI is English. Clients should use the HTTP status and `success` field rather than matching the message text.

### Error Responses

Upload errors currently use a simpler structure and may not include `success` or `error_code`:

```json
{
  "error": "缺少JSON地图文件"
}
```

Common statuses:

| Status | Meaning |
| --- | --- |
| `400` | Missing metadata, missing JSON file, invalid extension/MIME type, size limit, or rejected metadata |
| `429` | Upload rate limit exceeded; response includes `resetIn` seconds |
| `500` | R2 upload, database, or unexpected server failure |
| `405` | `GET /api/upload` is not allowed |

The current rate limit is five upload attempts per IP per 60 seconds.

## GET /api/download/{id}

Downloads the map JSON associated with a map UUID.

Use the `download_url` returned by `/api/maps` instead of building this URL from an untrusted or manually copied ID.

### Example Request

```http
GET /api/download/c75a357d-da96-4ed4-8ebe-30f60a99e683 HTTP/1.1
Host: peakmap.top
```

### Success Response

- Status: `200 OK`
- `Content-Type: application/json`
- `Content-Disposition: attachment; filename*=UTF-8''...json`
- `Cache-Control: no-store`
- Body: raw map JSON bytes

Chinese map names receive an ASCII pinyin suffix in the downloaded filename when possible. The file content is unchanged.

### Error Responses

```json
{
  "error": "地图不存在"
}
```

| Status | Meaning |
| --- | --- |
| `400` | Missing map ID |
| `404` | Map ID does not exist |
| `429` | Download rate limit exceeded; response includes `resetIn` seconds |
| `500` | Storage or unexpected server failure |

The current rate limit is twenty download attempts per IP per 60 seconds.

Every successful download increments the map download counter.

## GET /api/r2/{key}

This is the public resource proxy used by returned `image_url` and `json_file_url` values. Clients normally do not need to construct this path themselves.

Examples:

```text
GET /api/r2/images/example.png
GET /api/r2/maps/example.json
```

Successful responses return the stored file bytes with its content type and long-lived immutable caching. Path traversal attempts return `400`; missing files return `404`.

## Unity / C# Integration Example

```csharp
string url = "https://peakmap.top/api/maps?page=1&page_size=12&sort=newest&lang=en";

using (UnityWebRequest request = UnityWebRequest.Get(url))
{
    yield return request.SendWebRequest();

    if (request.result != UnityWebRequest.Result.Success)
    {
        Debug.LogError(request.error);
        yield break;
    }

    MapsResponse response = JsonConvert.DeserializeObject<MapsResponse>(request.downloadHandler.text);
    foreach (MapEntry map in response.data)
    {
        using (UnityWebRequest mapRequest = UnityWebRequest.Get(map.download_url))
        {
            yield return mapRequest.SendWebRequest();
            byte[] jsonBytes = mapRequest.downloadHandler.data;
        }
    }
}
```

## Recommended Client Behavior

1. Call `/api/mod-versions` to populate a version selector.
2. Call `/api/maps` with explicit `page`, `page_size`, `sort`, and `lang` values.
3. Use `pagination.has_next` and `pagination.has_prev` for navigation.
4. Use `download_url` for map downloads.
5. Treat empty image URLs as a valid no-cover state.
6. Switch on `error_code` for GET endpoint failures; do not match localized text.
7. For upload/download `429` responses, wait for `resetIn` seconds before retrying.

