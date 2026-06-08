// meshoptimizer LOD helper for AcManager car LOD generation.
//
// Build (you wire this up): compile this file together with the meshoptimizer
// sources from https://github.com/zeux/meshoptimizer and link as MeshOptimizerLod.exe.
//
//   #include path must contain meshoptimizer.h (from meshoptimizer/src/).
//
// Interface mirrors ScriptMeshLab.py: reads kn5-style COLLADA, simplifies each
// <geometry> with meshopt_simplifyWithAttributes, writes COLLADA back in the
// same per-corner layout kn5.ExportCollada expects.
//
// Progress: numeric lines on stdout (0..100). Errors on stderr. Exit code != 0 on failure.

#include <meshoptimizer/meshoptimizer.h>
#pragma comment(lib, "meshoptimizer/meshoptimizer.lib")

#include <algorithm>
#include <cmath>
#include <cctype>
#include <cfloat>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <fstream>
#include <sstream>
#include <string>
#include <vector>

namespace {

struct Vertex {
    float px, py, pz;
    float nx, ny, nz;
    float tu, tv;
};

struct GeometryMesh {
    std::string id;
    std::string name;
    std::string material_symbol;
    std::vector<Vertex> corners; // 3 vertices per triangle, kn5 per-corner layout
};

struct SourceEntry {
    std::string id;
    std::vector<float> data;
};

struct Options {
    std::string input_path;
    std::string output_path;
    double target_perc = 0.5;
    double target_error = 0.01;
    double normal_weight = 0.5;
    double uv_weight = 0.0;
    int min_keep = 20;
    double min_target_perc = 0.0;
    bool permissive = false;
};

void err(const char* msg) {
    std::fprintf(stderr, "%s\n", msg);
    std::fflush(stderr);
}

void progress(double value) {
    std::printf("%.2f\n", value);
    std::fflush(stdout);
}

std::string read_file(const std::string& path) {
    std::ifstream in(path.c_str(), std::ios::binary);
    if (!in)
        return std::string();
    return std::string((std::istreambuf_iterator<char>(in)), std::istreambuf_iterator<char>());
}

bool write_file(const std::string& path, const std::string& data) {
    std::ofstream out(path.c_str(), std::ios::binary);
    if (!out)
        return false;
    out.write(data.data(), static_cast<std::streamsize>(data.size()));
    return static_cast<bool>(out);
}

std::string trim(const std::string& s) {
    size_t b = 0;
    while (b < s.size() && std::isspace(static_cast<unsigned char>(s[b])))
        ++b;
    size_t e = s.size();
    while (e > b && std::isspace(static_cast<unsigned char>(s[e - 1])))
        --e;
    return s.substr(b, e - b);
}

std::string attr_value(const std::string& tag, const char* name) {
    std::string key = std::string(name) + "=\"";
    size_t p = tag.find(key);
    if (p == std::string::npos)
        return std::string();
    p += key.size();
    size_t e = tag.find('"', p);
    if (e == std::string::npos)
        return std::string();
    return tag.substr(p, e - p);
}

std::vector<std::string> split_ws(const std::string& text) {
    std::vector<std::string> out;
    std::istringstream iss(text);
    std::string token;
    while (iss >> token)
        out.push_back(token);
    return out;
}

std::vector<float> parse_floats(const std::string& text) {
    std::vector<float> out;
    std::istringstream iss(text);
    float v = 0.f;
    while (iss >> v)
        out.push_back(v);
    return out;
}

std::string tag_name_from_open(const std::string& open_tag) {
    size_t i = (!open_tag.empty() && open_tag[0] == '<') ? 1u : 0u;
    size_t j = i;
    while (j < open_tag.size() && open_tag[j] != ' ' && open_tag[j] != '>' && open_tag[j] != '/')
        ++j;
    return open_tag.substr(i, j - i);
}

std::string find_tag_block(const std::string& xml, const std::string& open_tag, size_t start = 0) {
    size_t p = xml.find(open_tag, start);
    if (p == std::string::npos)
        return std::string();
    const std::string close_tag = "</" + tag_name_from_open(open_tag) + ">";
    size_t c = xml.find(close_tag, p);
    if (c == std::string::npos)
        return std::string();
    c += close_tag.size();
    return xml.substr(p, c - p);
}

std::string find_inner_text(const std::string& block, const char* tag) {
    std::string open = std::string("<") + tag;
    std::string close = std::string("</") + tag + ">";
    size_t p = block.find(open);
    if (p == std::string::npos)
        return std::string();
    size_t gt = block.find('>', p);
    if (gt == std::string::npos)
        return std::string();
    size_t c = block.find(close, gt + 1);
    if (c == std::string::npos)
        return std::string();
    return trim(block.substr(gt + 1, c - gt - 1));
}

std::vector<std::string> find_all_blocks(const std::string& xml, const char* tag) {
    std::vector<std::string> out;
    std::string open = std::string("<") + tag;
    std::string close = std::string("</") + tag + ">";
    size_t pos = 0;
    while (true) {
        size_t p = xml.find(open, pos);
        if (p == std::string::npos)
            break;
        // Tag boundary: "<input" must not match "<inputfoo" or attribute text.
        if (p + open.size() < xml.size()) {
            const char next = xml[p + open.size()];
            if (next != ' ' && next != '>' && next != '/')
            {
                pos = p + 1;
                continue;
            }
        }
        size_t gt = xml.find('>', p);
        if (gt == std::string::npos)
            break;
        if (gt > p && xml[gt - 1] == '/') {
            out.push_back(xml.substr(p, gt - p + 1));
            pos = gt + 1;
            continue;
        }
        size_t c = xml.find(close, gt + 1);
        if (c == std::string::npos)
            break;
        c += close.size();
        out.push_back(xml.substr(p, c - p));
        pos = c;
    }
    return out;
}

std::string element_open_tag(const std::string& elem) {
    const size_t lt = elem.find('<');
    if (lt == std::string::npos)
        return std::string();
    const size_t gt = elem.find('>', lt);
    if (gt == std::string::npos)
        return std::string();
    return elem.substr(lt, gt - lt + 1);
}

void strip_source_ref(std::string& ref) {
    if (!ref.empty() && ref[0] == '#')
        ref = ref.substr(1);
}

const std::vector<float>* find_source_data(const std::vector<SourceEntry>& source_list, const std::string& ref) {
    std::string key = ref;
    strip_source_ref(key);
    for (const SourceEntry& e : source_list) {
        if (e.id == key)
            return &e.data;
    }
    return nullptr;
}

int compute_target_faces(int face_count, double target_perc, int min_keep) {
    if (face_count <= 0)
        return 0;
    target_perc = std::max(0.001, std::min(1.0, target_perc));
    if (target_perc >= 1.0)
        return face_count;
    const double threshold = std::max(4.0, static_cast<double>(min_keep) / target_perc);
    double actual_perc = target_perc;
    if (face_count < static_cast<int>(threshold)) {
        const double t = static_cast<double>(face_count) / threshold;
        actual_perc = 1.0 - t * (1.0 - target_perc);
    }
    int target = static_cast<int>(std::lround(face_count * actual_perc));
    target = std::max(std::min(face_count, 4), std::min(face_count, target));
    return target;
}

int parse_kn5_geometry(const std::string& geom_block, GeometryMesh& out) {
    out = GeometryMesh();
    size_t lt = geom_block.find('<');
    if (lt == std::string::npos)
        return 1;
    size_t gt = geom_block.find('>', lt);
    if (gt == std::string::npos)
        return 2;
    const std::string open_tag = geom_block.substr(lt, gt - lt + 1);
    out.id = attr_value(open_tag, "id");
    out.name = attr_value(open_tag, "name");
    if (out.name.empty())
        out.name = out.id;

    const std::string mesh_block = find_tag_block(geom_block, "<mesh");
    if (mesh_block.empty())
        return 3;

    std::vector<float> pos;
    std::vector<float> norm;
    std::vector<float> uv;
    std::string material_symbol;

    std::vector<SourceEntry> source_list;
    for (const std::string& src : find_all_blocks(mesh_block, "source")) {
        SourceEntry entry;
        entry.id = attr_value(element_open_tag(src), "id");
        entry.data = parse_floats(find_inner_text(src, "float_array"));
        source_list.push_back(entry);
    }

    struct VertPosSource {
        std::string vertices_id;
        std::string position_source;
    };
    std::vector<VertPosSource> verts_pos_sources;
    for (const std::string& vs : find_all_blocks(mesh_block, "vertices")) {
        const std::string vid = attr_value(element_open_tag(vs), "id");
        for (const std::string& inp : find_all_blocks(vs, "input")) {
            if (attr_value(element_open_tag(inp), "semantic") == "POSITION") {
                std::string src = attr_value(element_open_tag(inp), "source");
                strip_source_ref(src);
                verts_pos_sources.push_back({ vid, src });
            }
        }
    }

    auto resolve_position_source = [&](const std::string& vertices_ref) -> std::string {
        std::string key = vertices_ref;
        if (!key.empty() && key[0] == '#')
            key = key.substr(1);
        for (const VertPosSource& entry : verts_pos_sources) {
            if (entry.vertices_id == key)
                return entry.position_source;
        }
        return std::string();
    };

    const char* primitive_tags[] = { "triangles", "polylist", "polygons" };
    for (const char* prim_name : primitive_tags) {
        for (const std::string& prim : find_all_blocks(mesh_block, prim_name)) {
            if (material_symbol.empty())
                material_symbol = attr_value(element_open_tag(prim), "material");

            struct InputInfo {
                std::string semantic;
                std::string source;
                int offset = 0;
            };
            std::vector<InputInfo> inputs;
            for (const std::string& inp : find_all_blocks(prim, "input")) {
                const std::string open = element_open_tag(inp);
                InputInfo ii;
                ii.semantic = attr_value(open, "semantic");
                ii.source = attr_value(open, "source");
                ii.offset = std::atoi(attr_value(open, "offset").c_str());
                inputs.push_back(ii);
            }
            if (inputs.empty())
                continue;

            int stride = 0;
            for (const InputInfo& ii : inputs)
                stride = std::max(stride, ii.offset + 1);

            int v_off = -1, n_off = -1, t_off = -1;
            for (const InputInfo& ii : inputs) {
                std::string src = ii.source;
                strip_source_ref(src);
                if (ii.semantic == "VERTEX") {
                    v_off = ii.offset;
                    const std::string pos_source = resolve_position_source(src);
                    if (!pos_source.empty()) {
                        if (const std::vector<float>* data = find_source_data(source_list, pos_source)) {
                            if (pos.empty())
                                pos = *data;
                        }
                    }
                } else if (ii.semantic == "POSITION") {
                    v_off = ii.offset;
                    if (const std::vector<float>* data = find_source_data(source_list, src)) {
                        if (pos.empty())
                            pos = *data;
                    }
                } else if (ii.semantic == "NORMAL") {
                    n_off = ii.offset;
                    if (const std::vector<float>* data = find_source_data(source_list, src)) {
                        if (norm.empty())
                            norm = *data;
                    }
                } else if (ii.semantic == "TEXCOORD") {
                    t_off = ii.offset;
                    if (const std::vector<float>* data = find_source_data(source_list, src)) {
                        if (uv.empty())
                            uv = *data;
                    }
                }
            }

            const std::vector<int> raw = [&]() {
                std::vector<int> values;
                const std::string p_text = find_inner_text(prim, "p");
                for (const std::string& tok : split_ws(p_text))
                    values.push_back(std::atoi(tok.c_str()));
                return values;
            }();

            if (raw.empty())
                continue;

            auto emit_corner = [&](int base) {
                const int v_i = v_off >= 0 ? raw[base + v_off] : 0;
                const int n_i = n_off >= 0 ? raw[base + n_off] : v_i;
                const int t_i = t_off >= 0 ? raw[base + t_off] : v_i;
                Vertex vtx = {};
                if (!pos.empty()) {
                    const int pi = (v_i % (static_cast<int>(pos.size()) / 3)) * 3;
                    vtx.px = pos[pi];
                    vtx.py = pos[pi + 1];
                    vtx.pz = pos[pi + 2];
                }
                if (!norm.empty()) {
                    const int ni = (n_i % (static_cast<int>(norm.size()) / 3)) * 3;
                    vtx.nx = norm[ni];
                    vtx.ny = norm[ni + 1];
                    vtx.nz = norm[ni + 2];
                } else {
                    vtx.nx = 0.f;
                    vtx.ny = 1.f;
                    vtx.nz = 0.f;
                }
                if (!uv.empty()) {
                    const int ti = (t_i % (static_cast<int>(uv.size()) / 2)) * 2;
                    vtx.tu = uv[ti];
                    vtx.tv = uv[ti + 1];
                }
                out.corners.push_back(vtx);
            };

            if (std::strcmp(prim_name, "polylist") == 0) {
                const std::vector<int> vcount = [&]() {
                    std::vector<int> values;
                    for (const std::string& tok : split_ws(find_inner_text(prim, "vcount")))
                        values.push_back(std::atoi(tok.c_str()));
                    return values;
                }();
                int cursor = 0;
                for (int vc : vcount) {
                    if (vc < 3) {
                        cursor += vc;
                        continue;
                    }
                    const int first = cursor;
                    for (int k = 1; k < vc - 1; ++k) {
                        emit_corner(first * stride);
                        emit_corner((cursor + k) * stride);
                        emit_corner((cursor + k + 1) * stride);
                    }
                    cursor += vc;
                }
            } else {
                const int n_corners = static_cast<int>(raw.size()) / stride;
                for (int c = 0; c < n_corners; ++c)
                    emit_corner(c * stride);
            }
        }
    }

    if (pos.empty()) 
        return 11;
    if (out.corners.empty())
        return 12;
    out.material_symbol = material_symbol;
    return 0;
}

std::string write_kn5_mesh(const GeometryMesh& geo) {
    std::ostringstream oss;
    const std::string& name = geo.name;
    const size_t n_v = geo.corners.size();
    const size_t n_tri = n_v / 3;

    oss << "    <mesh>\n";

    auto write_source = [&](const char* suffix, int stride, const char* p0, const char* p1, const char* p2,
            void (*getter)(const Vertex&, float out[3])) {
        const std::string sid = name + suffix;
        oss << "      <source id=\"" << sid << "\">\n";
        oss << "        <float_array id=\"" << sid << "-array\" count=\"" << (n_v * stride) << "\">";
        for (size_t i = 0; i < n_v; ++i) {
            float vals[3] = {};
            getter(geo.corners[i], vals);
            for (int k = 0; k < stride; ++k) {
                if (i > 0 || k > 0)
                    oss << ' ';
                oss << vals[k];
            }
        }
        oss << "</float_array>\n";
        oss << "        <technique_common><accessor source=\"#" << sid << "-array\" count=\"" << n_v
            << "\" stride=\"" << stride << "\">";
        oss << "<param name=\"" << p0 << "\" type=\"float\"/>";
        if (stride > 1)
            oss << "<param name=\"" << p1 << "\" type=\"float\"/>";
        if (stride > 2)
            oss << "<param name=\"" << p2 << "\" type=\"float\"/>";
        oss << "</accessor></technique_common>\n";
        oss << "      </source>\n";
    };

    write_source("-mesh-positions", 3, "X", "Y", "Z", [](const Vertex& v, float out[3]) {
        out[0] = v.px;
        out[1] = v.py;
        out[2] = v.pz;
    });
    write_source("-mesh-normals", 3, "X", "Y", "Z", [](const Vertex& v, float out[3]) {
        out[0] = v.nx;
        out[1] = v.ny;
        out[2] = v.nz;
    });
    write_source("-mesh-map-0", 2, "S", "T", "", [](const Vertex& v, float out[3]) {
        out[0] = v.tu;
        out[1] = v.tv;
    });

    oss << "      <vertices id=\"" << name << "-mesh-vertices\">\n";
    oss << "        <input semantic=\"POSITION\" source=\"#" << name << "-mesh-positions\"/>\n";
    oss << "      </vertices>\n";
    oss << "      <triangles";
    if (!geo.material_symbol.empty())
        oss << " material=\"" << geo.material_symbol << "\"";
    oss << " count=\"" << n_tri << "\">\n";
    oss << "        <input semantic=\"VERTEX\" source=\"#" << name << "-mesh-vertices\" offset=\"0\"/>\n";
    oss << "        <input semantic=\"NORMAL\" source=\"#" << name << "-mesh-normals\" offset=\"1\"/>\n";
    oss << "        <input semantic=\"TEXCOORD\" source=\"#" << name << "-mesh-map-0\" offset=\"2\" set=\"0\"/>\n";
    oss << "        <p>";
    for (size_t i = 0; i < n_v; ++i) {
        if (i > 0)
            oss << ' ';
        oss << i << ' ' << i << ' ' << i;
    }
    oss << "</p>\n";
    oss << "      </triangles>\n";
    oss << "    </mesh>\n";
    return oss.str();
}

bool simplify_geometry(GeometryMesh& geo, const Options& opt) {
    const size_t face_count = geo.corners.size() / 3;
    if (face_count == 0)
        return true;

    int target_faces = compute_target_faces(static_cast<int>(face_count), opt.target_perc, opt.min_keep);
    if (opt.min_target_perc > 0.0) {
        const int floor_faces = static_cast<int>(std::ceil(face_count * std::max(0.001, std::min(1.0, opt.min_target_perc))));
        target_faces = std::max(target_faces, floor_faces);
    }
    target_faces = std::min(static_cast<int>(face_count), target_faces);
    std::fprintf(stderr, "target_faces:%d, face_count:%d\n", target_faces, face_count);
    if (target_faces >= static_cast<int>(face_count))
        return true;

    std::vector<Vertex> vertices = geo.corners;
    std::vector<unsigned int> indices(vertices.size());
    for (size_t i = 0; i < indices.size(); ++i)
        indices[i] = static_cast<unsigned int>(i);

    std::vector<unsigned int> remap(vertices.size());
    const size_t vertex_count = meshopt_generateVertexRemap(
            remap.data(), indices.data(), indices.size(), vertices.data(), vertices.size(), sizeof(Vertex));

    std::vector<Vertex> welded(vertex_count);
    meshopt_remapVertexBuffer(welded.data(), vertices.data(), vertices.size(), sizeof(Vertex), remap.data());
    std::vector<unsigned int> welded_indices(indices.size());
    meshopt_remapIndexBuffer(welded_indices.data(), indices.data(), indices.size(), remap.data());

    const size_t target_index_count = static_cast<size_t>(target_faces) * 3;
    std::vector<unsigned int> simplified(indices.size());
    float result_error = 0.f;

    std::vector<float> attr_weights;
    attr_weights.push_back(static_cast<float>(opt.normal_weight));
    attr_weights.push_back(static_cast<float>(opt.normal_weight));
    attr_weights.push_back(static_cast<float>(opt.normal_weight));
    if (opt.uv_weight > 0.0) {
        attr_weights.push_back(static_cast<float>(opt.uv_weight));
        attr_weights.push_back(static_cast<float>(opt.uv_weight));
    }

    const unsigned int simplify_options = (opt.permissive ? meshopt_SimplifyPermissive : 0) | meshopt_SimplifyPrune;
    size_t simplified_count = 0;

    if (opt.uv_weight > 0.0) {
        simplified_count = meshopt_simplifyWithAttributes(
                simplified.data(), welded_indices.data(), welded_indices.size(), &welded[0].px, welded.size(),
                sizeof(Vertex), &welded[0].tu, sizeof(Vertex), attr_weights.data(), static_cast<unsigned int>(attr_weights.size()),
                nullptr, target_index_count, static_cast<float>(opt.target_error), simplify_options, &result_error);
    } else {
        simplified_count = meshopt_simplifyWithAttributes(
                simplified.data(), welded_indices.data(), welded_indices.size(), &welded[0].px, welded.size(),
                sizeof(Vertex), &welded[0].nx, sizeof(Vertex), attr_weights.data(), 3, nullptr, target_index_count,
                static_cast<float>(opt.target_error), simplify_options, &result_error);
    }

    if (simplified_count == 0)
        return true;
    simplified.resize(simplified_count);

    meshopt_optimizeVertexCache(simplified.data(), simplified.data(), simplified.size(), welded.size());
    const size_t compact_vertices = meshopt_optimizeVertexFetch(
            welded.data(), simplified.data(), simplified.size(), welded.data(), welded.size(), sizeof(Vertex));
    welded.resize(compact_vertices);

    geo.corners.clear();
    geo.corners.reserve(simplified.size());
    for (unsigned int idx : simplified)
        geo.corners.push_back(welded[idx]);

    std::fprintf(stderr, "  %s: %zu -> %zu triangles (error %.4f)\n", geo.name.c_str(), face_count, geo.corners.size() / 3,
            result_error);
    return true;
}

bool parse_args(int argc, char** argv, Options& opt) {
    for (int i = 1; i < argc; ++i) {
        const std::string arg = argv[i];
        auto need_value = [&](const char* name) -> const char* {
            if (arg != name)
                return nullptr;
            if (i + 1 >= argc) {
                err("Missing value for argument");
                std::exit(2);
            }
            return argv[++i];
        };
        if (const char* v = need_value("--input"))
            opt.input_path = v;
        else if (const char* v = need_value("--output"))
            opt.output_path = v;
        else if (const char* v = need_value("--target-perc"))
            opt.target_perc = std::atof(v);
        else if (const char* v = need_value("--target-error"))
            opt.target_error = std::atof(v);
        else if (const char* v = need_value("--normal-weight"))
            opt.normal_weight = std::atof(v);
        else if (const char* v = need_value("--uv-weight"))
            opt.uv_weight = std::atof(v);
        else if (const char* v = need_value("--min-keep"))
            opt.min_keep = std::atoi(v);
        else if (const char* v = need_value("--min-target-perc"))
            opt.min_target_perc = std::atof(v);
        else if (arg == "--permissive")
            opt.permissive = true;
        else {
            err(("Unknown argument: " + arg).c_str());
            return false;
        }
    }
    if (opt.input_path.empty() || opt.output_path.empty()) {
        err("Usage: MeshOptimizerLod --input <file.dae> --output <file.dae> --target-perc <0..1> [options]");
        return false;
    }
    return true;
}

} // namespace

int main(int argc, char** argv) {
    Options opt;
    if (!parse_args(argc, argv, opt))
        return 2;

    const std::string xml = read_file(opt.input_path);
    if (xml.empty()) {
        err("DAEParseError: failed to read input DAE");
        return 3;
    }

    const std::string lib_geo = find_tag_block(xml, "<library_geometries");
    if (lib_geo.empty()) {
        err("DAEFormatError: <library_geometries> is missing");
        return 3;
    }

    const std::vector<std::string> geometry_blocks = find_all_blocks(lib_geo, "geometry");
    if (geometry_blocks.empty()) {
        err("DAEFormatError: no <geometry> elements found");
        return 3;
    }

    std::fprintf(stderr, "meshoptimizer: decimating %zu geometries (target keep ratio %.2f%%)\n", geometry_blocks.size(),
            opt.target_perc * 100.0);

    std::vector<GeometryMesh> geometries;
    geometries.reserve(geometry_blocks.size());
    for (const std::string& block : geometry_blocks) {
        GeometryMesh geo;
        if (auto err = parse_kn5_geometry(block, geo)) {
            std::fprintf(stderr, "meshoptimizer: skipping geometry (parse failed, err=%d)\n", err);
            geometries.push_back(GeometryMesh());
            continue;
        }
        geometries.push_back(geo);
    }

    const size_t total = geometry_blocks.size();
    for (size_t idx = 0; idx < total; ++idx) {
        GeometryMesh& geo = geometries[idx];
        if (!geo.corners.empty() && !simplify_geometry(geo, opt)) {
            std::fprintf(stderr, "meshoptimizer: simplification failed on %s\n", geo.name.c_str());
        }
        progress((idx + 1) * 100.0 / static_cast<double>(total));
    }

    const size_t lib_pos = xml.find("<library_geometries");
    const size_t lib_close = xml.find("</library_geometries>");
    if (lib_pos == std::string::npos || lib_close == std::string::npos) {
        err("DAEFormatError: could not locate geometries library for rewrite");
        return 5;
    }
    const size_t lib_close_end = lib_close + std::strlen("</library_geometries>");

    std::ostringstream lib_out;
    lib_out << "<library_geometries>\n";
    for (size_t idx = 0; idx < geometry_blocks.size(); ++idx) {
        const std::string& block = geometry_blocks[idx];
        size_t lt = block.find('<');
        size_t gt = block.find('>', lt);
        const std::string open_tag = block.substr(lt, gt - lt + 1);
        const std::string id = attr_value(open_tag, "id");
        const std::string name = attr_value(open_tag, "name");
        lib_out << "  <geometry";
        if (!id.empty())
            lib_out << " id=\"" << id << "\"";
        if (!name.empty())
            lib_out << " name=\"" << name << "\"";
        lib_out << ">\n";
        if (!geometries[idx].corners.empty()) {
            lib_out << write_kn5_mesh(geometries[idx]);
        } else {
            // Parse failed or mesh was empty: copy original inner content verbatim.
            const size_t open_end = block.find('>');
            const size_t close_geo = block.rfind("</geometry>");
            if (open_end != std::string::npos && close_geo != std::string::npos && close_geo > open_end)
                lib_out << block.substr(open_end + 1, close_geo - open_end - 1);
        }
        lib_out << "  </geometry>\n";
    }
    lib_out << "</library_geometries>";

    std::string rebuilt = xml.substr(0, lib_pos) + lib_out.str() + xml.substr(lib_close_end);

    if (!write_file(opt.output_path, rebuilt)) {
        err("DAEWriteError: failed to write output DAE");
        return 5;
    }

    progress(100.0);
    return 0;
}
