// This file is a test fixture for pagination tests in Phase 9.
// It declares exactly 105 top-level classes in a dedicated namespace so
// integration tests can exercise the 100/105 boundary:
//   - page 1 (offset=0, maxResults=100) returns 100 classes, truncated=true, nextOffset=100
//   - page 2 (offset=100, maxResults=100) returns 5 classes, truncated=false, nextOffset=null
//
// Do not add/remove classes without updating the pagination assertions in
// Tests/Tools/ListNamespaceTypesToolTests.cs Pagination_* facts.

namespace ILSpy.Mcp.TestTargets.Pagination;

public class PaginationType001 { }
public class PaginationType002 { }
public class PaginationType003 { }
public class PaginationType004 { }
public class PaginationType005 { }
public class PaginationType006 { }
public class PaginationType007 { }
public class PaginationType008 { }
public class PaginationType009 { }
public class PaginationType010 { }
public class PaginationType011 { }
public class PaginationType012 { }
public class PaginationType013 { }
public class PaginationType014 { }
public class PaginationType015 { }
public class PaginationType016 { }
public class PaginationType017 { }
public class PaginationType018 { }
public class PaginationType019 { }
public class PaginationType020 { }
public class PaginationType021 { }
public class PaginationType022 { }
public class PaginationType023 { }
public class PaginationType024 { }
public class PaginationType025 { }
public class PaginationType026 { }
public class PaginationType027 { }
public class PaginationType028 { }
public class PaginationType029 { }
public class PaginationType030 { }
public class PaginationType031 { }
public class PaginationType032 { }
public class PaginationType033 { }
public class PaginationType034 { }
public class PaginationType035 { }
public class PaginationType036 { }
public class PaginationType037 { }
public class PaginationType038 { }
public class PaginationType039 { }
public class PaginationType040 { }
public class PaginationType041 { }
public class PaginationType042 { }
public class PaginationType043 { }
public class PaginationType044 { }
public class PaginationType045 { }
public class PaginationType046 { }
public class PaginationType047 { }
public class PaginationType048 { }
public class PaginationType049 { }
public class PaginationType050 { }
public class PaginationType051 { }
public class PaginationType052 { }
public class PaginationType053 { }
public class PaginationType054 { }
public class PaginationType055 { }
public class PaginationType056 { }
public class PaginationType057 { }
public class PaginationType058 { }
public class PaginationType059 { }
public class PaginationType060 { }
public class PaginationType061 { }
public class PaginationType062 { }
public class PaginationType063 { }
public class PaginationType064 { }
public class PaginationType065 { }
public class PaginationType066 { }
public class PaginationType067 { }
public class PaginationType068 { }
public class PaginationType069 { }
public class PaginationType070 { }
public class PaginationType071 { }
public class PaginationType072 { }
public class PaginationType073 { }
public class PaginationType074 { }
public class PaginationType075 { }
public class PaginationType076 { }
public class PaginationType077 { }
public class PaginationType078 { }
public class PaginationType079 { }
public class PaginationType080 { }
public class PaginationType081 { }
public class PaginationType082 { }
public class PaginationType083 { }
public class PaginationType084 { }
public class PaginationType085 { }
public class PaginationType086 { }
public class PaginationType087 { }
public class PaginationType088 { }
public class PaginationType089 { }
public class PaginationType090 { }
public class PaginationType091 { }
public class PaginationType092 { }
public class PaginationType093 { }
public class PaginationType094 { }
public class PaginationType095 { }
public class PaginationType096 { }
public class PaginationType097 { }
public class PaginationType098 { }
public class PaginationType099 { }
public class PaginationType100 { }
public class PaginationType101 { }
public class PaginationType102 { }
public class PaginationType103 { }
public class PaginationType104 { }
public class PaginationType105 { }
