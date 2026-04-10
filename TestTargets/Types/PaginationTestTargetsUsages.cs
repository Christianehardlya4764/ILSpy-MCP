// Phase 10 pagination fixture sibling file — Usages scenario.
// Declares >=105 call sites of IUsagesTarget.Ping so find_usages pagination can
// be exercised at the 100/105 boundary. Do not add/remove call sites without
// updating the Tests/Tools/FindUsagesToolTests.Pagination_* assertions.

namespace ILSpy.Mcp.TestTargets.Pagination.Usages
{
    // Target interface with a single method. find_usages against IUsagesTarget.Ping
    // will return one match per caller site below.
    public interface IUsagesTarget { void Ping(); }

    public sealed class UsagesHelper : IUsagesTarget { public void Ping() { } }

    // 35 caller methods each calling Ping() once.
    public class UsagesCallerA
    {
        private readonly IUsagesTarget _t = new UsagesHelper();
        public void Call001() { _t.Ping(); }
        public void Call002() { _t.Ping(); }
        public void Call003() { _t.Ping(); }
        public void Call004() { _t.Ping(); }
        public void Call005() { _t.Ping(); }
        public void Call006() { _t.Ping(); }
        public void Call007() { _t.Ping(); }
        public void Call008() { _t.Ping(); }
        public void Call009() { _t.Ping(); }
        public void Call010() { _t.Ping(); }
        public void Call011() { _t.Ping(); }
        public void Call012() { _t.Ping(); }
        public void Call013() { _t.Ping(); }
        public void Call014() { _t.Ping(); }
        public void Call015() { _t.Ping(); }
        public void Call016() { _t.Ping(); }
        public void Call017() { _t.Ping(); }
        public void Call018() { _t.Ping(); }
        public void Call019() { _t.Ping(); }
        public void Call020() { _t.Ping(); }
        public void Call021() { _t.Ping(); }
        public void Call022() { _t.Ping(); }
        public void Call023() { _t.Ping(); }
        public void Call024() { _t.Ping(); }
        public void Call025() { _t.Ping(); }
        public void Call026() { _t.Ping(); }
        public void Call027() { _t.Ping(); }
        public void Call028() { _t.Ping(); }
        public void Call029() { _t.Ping(); }
        public void Call030() { _t.Ping(); }
        public void Call031() { _t.Ping(); }
        public void Call032() { _t.Ping(); }
        public void Call033() { _t.Ping(); }
        public void Call034() { _t.Ping(); }
        public void Call035() { _t.Ping(); }
    }

    // 35 caller methods each calling Ping() once.
    public class UsagesCallerB
    {
        private readonly IUsagesTarget _t = new UsagesHelper();
        public void Call036() { _t.Ping(); }
        public void Call037() { _t.Ping(); }
        public void Call038() { _t.Ping(); }
        public void Call039() { _t.Ping(); }
        public void Call040() { _t.Ping(); }
        public void Call041() { _t.Ping(); }
        public void Call042() { _t.Ping(); }
        public void Call043() { _t.Ping(); }
        public void Call044() { _t.Ping(); }
        public void Call045() { _t.Ping(); }
        public void Call046() { _t.Ping(); }
        public void Call047() { _t.Ping(); }
        public void Call048() { _t.Ping(); }
        public void Call049() { _t.Ping(); }
        public void Call050() { _t.Ping(); }
        public void Call051() { _t.Ping(); }
        public void Call052() { _t.Ping(); }
        public void Call053() { _t.Ping(); }
        public void Call054() { _t.Ping(); }
        public void Call055() { _t.Ping(); }
        public void Call056() { _t.Ping(); }
        public void Call057() { _t.Ping(); }
        public void Call058() { _t.Ping(); }
        public void Call059() { _t.Ping(); }
        public void Call060() { _t.Ping(); }
        public void Call061() { _t.Ping(); }
        public void Call062() { _t.Ping(); }
        public void Call063() { _t.Ping(); }
        public void Call064() { _t.Ping(); }
        public void Call065() { _t.Ping(); }
        public void Call066() { _t.Ping(); }
        public void Call067() { _t.Ping(); }
        public void Call068() { _t.Ping(); }
        public void Call069() { _t.Ping(); }
        public void Call070() { _t.Ping(); }
    }

    // 35 caller methods each calling Ping() once.
    public class UsagesCallerC
    {
        private readonly IUsagesTarget _t = new UsagesHelper();
        public void Call071() { _t.Ping(); }
        public void Call072() { _t.Ping(); }
        public void Call073() { _t.Ping(); }
        public void Call074() { _t.Ping(); }
        public void Call075() { _t.Ping(); }
        public void Call076() { _t.Ping(); }
        public void Call077() { _t.Ping(); }
        public void Call078() { _t.Ping(); }
        public void Call079() { _t.Ping(); }
        public void Call080() { _t.Ping(); }
        public void Call081() { _t.Ping(); }
        public void Call082() { _t.Ping(); }
        public void Call083() { _t.Ping(); }
        public void Call084() { _t.Ping(); }
        public void Call085() { _t.Ping(); }
        public void Call086() { _t.Ping(); }
        public void Call087() { _t.Ping(); }
        public void Call088() { _t.Ping(); }
        public void Call089() { _t.Ping(); }
        public void Call090() { _t.Ping(); }
        public void Call091() { _t.Ping(); }
        public void Call092() { _t.Ping(); }
        public void Call093() { _t.Ping(); }
        public void Call094() { _t.Ping(); }
        public void Call095() { _t.Ping(); }
        public void Call096() { _t.Ping(); }
        public void Call097() { _t.Ping(); }
        public void Call098() { _t.Ping(); }
        public void Call099() { _t.Ping(); }
        public void Call100() { _t.Ping(); }
        public void Call101() { _t.Ping(); }
        public void Call102() { _t.Ping(); }
        public void Call103() { _t.Ping(); }
        public void Call104() { _t.Ping(); }
        public void Call105() { _t.Ping(); }
    }
}
