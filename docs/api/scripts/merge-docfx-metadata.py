import shutil
import sys
from pathlib import Path

import yaml


MANAGED_REFERENCE = "### YamlMime:ManagedReference"
TABLE_OF_CONTENTS = "### YamlMime:TableOfContent"


def read_yaml(path, mime):
    with path.open(encoding="utf-8-sig") as stream:
        if stream.readline().strip() != mime:
            raise ValueError(f"{path} is missing its {mime} header")
        return yaml.safe_load(stream)


def write_yaml(path, mime, document):
    with path.open("w", encoding="utf-8", newline="\n") as stream:
        stream.write(mime + "\n")
        yaml.safe_dump(document, stream, sort_keys=False, allow_unicode=True)


def combine_by_uid(existing, incoming):
    values = {item["uid"]: item for item in existing}
    for item in incoming:
        values.setdefault(item["uid"], item)
    return list(values.values())


def merge_page(existing, incoming):
    page = read_yaml(existing, MANAGED_REFERENCE)
    addition = read_yaml(incoming, MANAGED_REFERENCE)
    root = page["items"][0]
    other = addition["items"][0]
    if root["uid"] != other["uid"] or root["type"] != other["type"]:
        raise ValueError(f"DocFX generated conflicting pages named {existing.name}")

    # Match DocFX's combined extraction when multiple assemblies share a namespace or class.
    for key in ("children", "extensionMethods"):
        if key in root or key in other:
            root[key] = sorted(set(root.get(key, []) + other.get(key, [])))

    page["items"] = combine_by_uid(page["items"], addition["items"])
    page["references"] = combine_by_uid(page.get("references", []), addition.get("references", []))
    write_yaml(existing, MANAGED_REFERENCE, page)


def merge_metadata(batches, output):
    output.mkdir(parents=True, exist_ok=True)
    namespaces = {}
    toc = None
    for batch in sorted(batches.glob("batch-*")):
        if not batch.is_dir():
            continue

        for source in batch.glob("*.yml"):
            if source.name == "toc.yml":
                data = read_yaml(source, TABLE_OF_CONTENTS)
                if toc is None:
                    toc = data
                for item in data["items"]:
                    namespace = namespaces.setdefault(item["uid"], {**item, "items": []})
                    namespace["items"].extend(item.get("items", []))
                continue

            target = output / source.name
            if target.exists():
                merge_page(target, source)
            else:
                shutil.copyfile(source, target)

    if not namespaces:
        raise ValueError(f"No DocFX navigation was generated in {batches}")

    for namespace in namespaces.values():
        children = {item["uid"]: item for item in namespace["items"]}
        namespace["items"] = [children[uid] for uid in sorted(children)]
    toc["items"] = [namespaces[uid] for uid in sorted(namespaces)]
    write_yaml(output / "toc.yml", TABLE_OF_CONTENTS, toc)


if __name__ == "__main__":
    merge_metadata(Path(sys.argv[1]), Path(sys.argv[2]))
