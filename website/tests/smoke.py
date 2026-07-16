from pathlib import Path
import os
import re

from playwright.sync_api import Page, expect, sync_playwright


BASE_URL = os.environ.get("BASE_URL", "http://127.0.0.1:3000")
SCREENSHOTS = Path(__file__).parent / "screenshots-navigation-waterfall"
VIEWPORTS = [
    ("desktop", 1440, 900),
    ("tablet-wide", 1024, 900),
    ("tablet", 768, 900),
    ("mobile", 390, 844),
]


def settle_reveals_for_screenshot(page: Page) -> None:
    page.locator("[data-text-reveal]").evaluate_all(
        "(nodes) => nodes.forEach((node) => node.classList.add('isTextRevealed'))"
    )
    page.wait_for_timeout(80)


def assert_page_health(page: Page, path: str, expected_lang: str) -> None:
    console_errors: list[str] = []
    page.on(
        "console",
        lambda message: console_errors.append(message.text)
        if message.type == "error"
        else None,
    )

    response = page.goto(BASE_URL + path, wait_until="networkidle")
    assert response is not None and response.ok, f"{path} did not load successfully"
    expect(page.locator("html")).to_have_attribute("lang", expected_lang)
    expect(page.locator("h1")).to_have_count(1)
    expect(page.locator("main")).to_be_visible()

    overflow = page.evaluate(
        "() => document.documentElement.scrollWidth - window.innerWidth"
    )
    assert overflow <= 1, f"{path} overflows horizontally by {overflow}px"

    page.evaluate(
        """async () => {
          for (let y = 0; y < document.documentElement.scrollHeight; y += 600) {
            window.scrollTo(0, y);
            await new Promise((resolve) => setTimeout(resolve, 30));
          }
          await Promise.all([...document.images].map((image) => {
            if (image.complete && image.naturalWidth > 0) return Promise.resolve();
            return new Promise((resolve) => {
              image.addEventListener('load', resolve, { once: true });
              image.addEventListener('error', resolve, { once: true });
              setTimeout(resolve, 3000);
            });
          }));
          window.scrollTo(0, 0);
        }"""
    )
    page.wait_for_load_state("networkidle")
    broken_images = page.locator("img").evaluate_all(
        "(images) => images.filter((image) => !image.complete || image.naturalWidth === 0).map((image) => image.currentSrc || image.src)"
    )
    assert not broken_images, f"{path} contains broken images: {broken_images}"
    store_hrefs = page.locator("a").evaluate_all(
        "(links) => links.map((link) => link.href).filter((href) => href.includes('apps.microsoft.com'))"
    )
    assert store_hrefs, f"{path} is missing the Microsoft Store link"
    assert not console_errors, f"{path} console errors: {console_errors}"


def test_interactions(page: Page, locale: str) -> None:
    labels = (
        {
            "idle": "空闲",
            "near": "鼠标靠近",
            "layout_c": "C 双态展开",
        }
        if locale == "zh"
        else {
            "idle": "Idle",
            "near": "Pointer nearby",
            "layout_c": "C dual-state",
        }
    )

    island = page.get_by_test_id("demo-island")
    page.get_by_role("button", name=labels["idle"], exact=True).click()
    expect(island).to_have_class(re.compile(r"\bisIdle\b"))

    page.get_by_role("button", name=labels["layout_c"], exact=True).click()
    expect(island).to_have_class(re.compile(r"\bisLayoutC\b"))

    page.get_by_role("button", name=labels["near"], exact=True).click()
    expect(island).to_have_class(re.compile(r"\bisNear\b"))

    faq = page.locator(".faqItem").first
    faq_button = faq.get_by_role("button")
    faq_button.click()
    expect(faq_button).to_have_attribute("aria-expanded", "true")
    page.wait_for_timeout(80)
    opening_height = faq.locator(".faqAnswer").evaluate(
        "(answer) => answer.getBoundingClientRect().height"
    )
    page.wait_for_timeout(520)
    open_height = faq.locator(".faqAnswer").evaluate(
        "(answer) => answer.getBoundingClientRect().height"
    )
    reduced_motion = page.evaluate(
        "matchMedia('(prefers-reduced-motion: reduce)').matches"
    )
    if reduced_motion:
        assert open_height > 0, "FAQ answers should still open without animation"
    else:
        assert 0 < opening_height < open_height, (
            "FAQ answers should expand progressively"
        )

    page.reload(wait_until="networkidle")
    page.keyboard.press("Tab")
    focus_data = page.evaluate(
        """() => {
          const node = document.activeElement;
          const style = window.getComputedStyle(node);
          return {
            tag: node?.tagName,
            outline: style.outlineStyle,
            width: node?.getBoundingClientRect().width || 0,
            height: node?.getBoundingClientRect().height || 0
          };
        }"""
    )
    assert focus_data["tag"] == "A", "The first keyboard target should be the skip link"
    assert focus_data["outline"] != "none", "Keyboard focus must be visible"


def test_smooth_section_snap(page: Page) -> None:
    page.goto(BASE_URL + "/", wait_until="networkidle")
    expect(page.locator("html")).to_have_attribute("data-snap-scroll", "enabled")

    sections = page.locator("[data-snap-section]")
    assert sections.count() >= 8, "The home page should expose major scroll anchors"
    first_metrics = sections.nth(0).evaluate(
        "(section) => ({ top: section.offsetTop, height: section.offsetHeight })"
    )
    next_top = sections.nth(1).evaluate("(section) => section.offsetTop")
    viewport_height = page.evaluate("window.innerHeight")

    if first_metrics["height"] > viewport_height + 24:
        page.mouse.wheel(0, 240)
        page.wait_for_timeout(180)
        in_section_scroll = page.evaluate("window.scrollY")
        assert 0 < in_section_scroll < next_top, (
            "Long sections must keep ordinary scrolling before their bottom edge"
        )
        page.evaluate(
            "({ top, height }) => window.scrollTo(0, top + height - window.innerHeight)",
            first_metrics,
        )
        page.wait_for_timeout(80)

    page.mouse.wheel(0, 240)
    page.wait_for_timeout(420)
    early_scroll_top = page.evaluate("window.scrollY")
    start_top = max(0, first_metrics["top"] + first_metrics["height"] - viewport_height)
    progress = (early_scroll_top - start_top) / max(1, next_top - start_top)
    assert 0 < progress < 0.22, (
        "The longer easing should begin more slowly than a linear transition"
    )

    page.wait_for_timeout(1600)
    scroll_top = page.evaluate("window.scrollY")
    assert abs(scroll_top - next_top) <= 3, (
        f"Smooth scrolling should settle on the next section; "
        f"expected {next_top}, got {scroll_top}"
    )

    page.get_by_role("link", name="主页", exact=True).click()
    page.wait_for_timeout(420)
    nav_progress_top = page.evaluate("window.scrollY")
    assert 0 < nav_progress_top < scroll_top, (
        "Navigation clicks should use the nonlinear page animation"
    )
    page.wait_for_timeout(1800)
    assert abs(page.evaluate("window.scrollY")) <= 3

    for selector, next_selector in [
        ("#faq", ".closingSection"),
        (".closingSection", ".siteFooter"),
        (".siteFooter", None),
    ]:
        alignment = page.locator(selector).evaluate(
            """(section, nextSelector) => {
              const top = section.getBoundingClientRect().top + window.scrollY;
              window.scrollTo(0, top);
              const next = nextSelector ? document.querySelector(nextSelector) : null;
              return {
                top,
                height: section.offsetHeight,
                nextTop: next
                  ? next.getBoundingClientRect().top + window.scrollY
                  : null
              };
            }""",
            next_selector,
        )
        page.wait_for_timeout(60)
        settled_top = page.evaluate("window.scrollY")
        assert abs(settled_top - alignment["top"]) <= 3
        assert alignment["height"] >= viewport_height - 1, (
            f"{selector} should occupy at least one desktop viewport"
        )
        if alignment["nextTop"] is not None:
            assert alignment["nextTop"] - settled_top >= viewport_height - 1, (
                f"The next page should not peek into {selector}"
            )


def test_navigation_and_orbit(page: Page) -> None:
    page.goto(BASE_URL + "/", wait_until="networkidle")
    expect(page.locator(".heroSupport .buttonRow .button")).to_have_count(1)
    expect(page.locator(".heroSupport a[href*='apps.microsoft.com']")).to_have_count(1)
    expect(page.locator(".heroSupport a[href*='github.com']")).to_have_count(0)
    labels = page.locator(".desktopNavLinks a").all_text_contents()
    assert labels == [
        "主页",
        "新功能",
        "用户激励计划",
        "Microsoft Store",
    ]
    assert page.locator(".mobileMenuPanel > a").all_text_contents()[:4] == labels
    feature_link = page.locator(".desktopNavLinks .navFeatureLink")
    store_link = page.locator(".desktopNavLinks .navStoreLink")
    expect(feature_link).to_have_count(1)
    expect(feature_link.locator("svg")).to_have_count(0)
    expect(store_link.locator("svg")).to_have_count(1)
    store_arrow_stroke = store_link.locator("path").evaluate(
        "(path) => getComputedStyle(path).stroke"
    )
    assert store_arrow_stroke == "rgb(20, 20, 19)"

    page.goto(BASE_URL + "/en", wait_until="networkidle")
    assert page.locator(".desktopNavLinks a").all_text_contents() == [
        "Home",
        "What's new",
        "Community rewards",
        "Microsoft Store",
    ]
    assert page.locator(".mobileMenuPanel > a").all_text_contents()[:4] == [
        "Home",
        "What's new",
        "Community rewards",
        "Microsoft Store",
    ]
    expect(page.locator(".desktopNavLinks .navFeatureLink svg")).to_have_count(0)
    expect(page.locator(".desktopNavLinks .navStoreLink svg")).to_have_count(1)

    page.goto(BASE_URL + "/", wait_until="networkidle")
    player_orbit = page.locator(".playerOrbit")
    player_orbit.hover()

    orbiting_player = page.locator(".playerPill1")
    initial_position = orbiting_player.evaluate(
        "(pill) => ({ x: pill.getBoundingClientRect().x, y: pill.getBoundingClientRect().y })"
    )
    page.wait_for_timeout(700)
    later_position = orbiting_player.evaluate(
        "(pill) => ({ x: pill.getBoundingClientRect().x, y: pill.getBoundingClientRect().y })"
    )
    assert initial_position != later_position, (
        "White player pills should keep moving along the arc while it is hovered"
    )

    orbit_metrics = player_orbit.evaluate(
        "(orbit) => ({ height: orbit.offsetHeight, overflow: getComputedStyle(orbit).overflow })"
    )
    assert orbit_metrics["height"] == 340
    assert orbit_metrics["overflow"] == "hidden"
    visible_fractions = page.locator(".playerArcLines ellipse").evaluate_all(
        """(ellipses) => ellipses.map((ellipse) => {
          const cy = Number(ellipse.getAttribute('cy'));
          const ry = Number(ellipse.getAttribute('ry'));
          return (340 - (cy - ry)) / (2 * ry);
        })"""
    )
    assert all(abs(fraction - 2 / 3) <= 0.001 for fraction in visible_fractions), (
        f"Each ellipse should retain its upper two thirds, got {visible_fractions}"
    )
    visible_pills = page.locator(".playerPill:not(.playerPill7)").evaluate_all(
        """(pills) => pills.filter((pill) => {
          const rect = pill.getBoundingClientRect();
          const orbit = pill.parentElement.getBoundingClientRect();
          return rect.right > orbit.left && rect.left < orbit.right
            && rect.bottom > orbit.top && rect.top < orbit.bottom;
        }).length"""
    )
    assert 5 <= visible_pills <= 6, (
        f"The cropped arc should show five or six queued player pills, got {visible_pills}"
    )
    middle_pill_index = page.locator(".playerPill:not(.playerPill7)").evaluate_all(
        """(pills) => {
          const orbit = pills[0].parentElement.getBoundingClientRect();
          return pills.findIndex((pill) => {
            const rect = pill.getBoundingClientRect();
            const center = rect.left + rect.width / 2;
            return center > orbit.left + orbit.width * 0.25
              && center < orbit.left + orbit.width * 0.65;
          });
        }"""
    )
    assert middle_pill_index >= 0
    middle_pill = page.locator(".playerPill:not(.playerPill7)").nth(middle_pill_index)
    middle_x = middle_pill.evaluate(
        "(pill) => pill.getBoundingClientRect().left + pill.getBoundingClientRect().width / 2"
    )
    page.wait_for_timeout(500)
    assert middle_pill.evaluate(
        "(pill) => pill.getBoundingClientRect().left + pill.getBoundingClientRect().width / 2"
    ) > middle_x, "Player pills should travel from the left queue to the right edge"
    section_height = page.locator(".compatibilitySection").evaluate(
        "(section) => section.offsetHeight"
    )
    assert abs(section_height - page.evaluate("innerHeight")) <= 1

    center_error = page.locator(".playerPill7").evaluate(
        """(pill) => {
          const orbit = pill.parentElement.getBoundingClientRect();
          const rect = pill.getBoundingClientRect();
          return {
            x: Math.abs((rect.left + rect.width / 2) - (orbit.left + orbit.width / 2)),
            y: Math.abs((rect.top + rect.height / 2) - (orbit.top + orbit.height / 2))
          };
        }"""
    )
    assert center_error["x"] <= 1 and center_error["y"] <= 1, (
        f"The SMTC pill should be centered within 1px, got {center_error}"
    )


def test_selective_text_reveal(page: Page, reduced_motion: bool = False) -> None:
    page.goto(BASE_URL + "/?motion-test=1", wait_until="networkidle")
    page.evaluate("window.scrollTo(0, 0)")
    page.wait_for_timeout(850)
    reveal_targets = page.locator("[data-text-reveal]")
    all_copy = page.locator("h1, h2, h3, p")
    assert 8 <= reveal_targets.count() < all_copy.count(), (
        "Text reveals should be reserved for a small set of chapter headings"
    )
    expect(page.locator(".heroSupport > p[data-text-reveal]")).to_have_count(0)
    expect(page.locator(".faqQuestion[data-text-reveal]")).to_have_count(0)

    modules_title = page.locator("#modules h2[data-text-reveal]")
    if reduced_motion:
        assert modules_title.evaluate("(node) => getComputedStyle(node).opacity") == "1"
        duration = modules_title.evaluate(
            "(node) => getComputedStyle(node).transitionDuration"
        )
        assert duration in {"0s", "0.00001s", "1e-05s"}
        return

    expect(page.locator("html")).to_have_class(re.compile(r"\btextRevealReady\b"))
    assert not modules_title.evaluate(
        "(node) => node.classList.contains('isTextRevealed')"
    ), "Off-screen headings should wait until their section enters the viewport"
    hidden_state = modules_title.evaluate(
        "(node) => ({ opacity: getComputedStyle(node).opacity, transform: getComputedStyle(node).transform })"
    )
    assert float(hidden_state["opacity"]) <= 0.02
    assert hidden_state["transform"] != "none"

    modules_title.scroll_into_view_if_needed()
    page.wait_for_timeout(80)
    expect(modules_title).to_have_class(re.compile(r"\bisTextRevealed\b"))
    page.wait_for_timeout(1050)
    assert modules_title.evaluate("(node) => getComputedStyle(node).opacity") == "1"


def test_incentive_page(page: Page, path: str, lang: str, mobile: bool = False) -> None:
    errors: list[str] = []
    page.on("console", lambda message: errors.append(message.text) if message.type == "error" else None)
    page.route(
        "**/api/incentives/public",
        lambda route: route.fulfill(
            status=200,
            content_type="application/json",
            body=(
                '{"configured":true,"suggestions":['
                '{"id":"21b0d5ea-8d2e-4d1e-91aa-4ad6ee01b001","nickname":"岛民小林","title":"自动切换歌词布局",'
                '"body":"根据窗口状态切换单行和双行歌词。","created_at":"2026-07-14T00:00:00Z",'
                '"like_count":12,"liked":false,'
                '"attachment":{"name":"演示.mp4","type":"video/mp4","url":"https://example.com/demo.mp4"}},'
                '{"id":"21b0d5ea-8d2e-4d1e-91aa-4ad6ee01b002","nickname":"青柠","title":"专注模式",'
                '"body":"全屏工作时只显示当前一句歌词。","created_at":"2026-07-13T00:00:00Z","like_count":8,"liked":false},'
                '{"id":"21b0d5ea-8d2e-4d1e-91aa-4ad6ee01b003","nickname":"Sea","title":"播放器快速切换",'
                '"body":"多个会话同时存在时可以快速锁定播放器。","created_at":"2026-07-12T00:00:00Z","like_count":5,"liked":true},'
                '{"id":"21b0d5ea-8d2e-4d1e-91aa-4ad6ee01b004","nickname":"九月","title":"布局预设",'
                '"body":"为不同桌面保存独立的歌词岛布局。","created_at":"2026-07-11T00:00:00Z","like_count":3,"liked":false}'
                '],"previews":['
                '{"id":"preview-1","version":"v2.1 Preview","title_zh":"更安静的桌面交互",'
                '"title_en":"Quieter desktop interactions","body_zh":"继续打磨避让和收起体验。",'
                '"body_en":"More polish for avoidance and retraction.","highlights_zh":["更细致的鼠标避让"],'
                '"highlights_en":["More nuanced pointer avoidance"],"target_date":"2026-09-01",'
                '"status":"published","created_at":"2026-07-14T00:00:00Z",'
                '"updated_at":"2026-07-14T00:00:00Z","published_at":"2026-07-14T00:00:00Z"}]}'
            ),
        ),
    )
    page.route(
        "**/api/incentives/likes",
        lambda route: route.fulfill(
            status=200,
            content_type="application/json",
            body='{"liked":true,"like_count":13}',
        ),
    )
    response = page.goto(BASE_URL + path, wait_until="networkidle")
    assert response is not None and response.ok
    expect(page.locator("html")).to_have_attribute("lang", lang)
    expect(page.locator("h1")).to_have_count(1)
    assert page.evaluate("document.documentElement.scrollWidth - innerWidth") <= 1
    assert page.locator(".acceptedCard").count() >= 16
    assert 0 < page.locator(".acceptedAttachment").count() < page.locator(".acceptedCard").count()
    expect(page.locator(".acceptedTime").first).to_be_visible()
    expect(page.locator(".acceptedWaterfallColumn")).to_have_count(4)
    visible_columns = page.locator(".acceptedWaterfallColumn").evaluate_all(
        "(columns) => columns.filter((column) => getComputedStyle(column).display !== 'none').length"
    )
    assert visible_columns == (2 if mobile else 4)
    mask = page.locator(".acceptedWaterfallViewport").evaluate(
        "(node) => getComputedStyle(node).maskImage || getComputedStyle(node).webkitMaskImage"
    )
    reduced_motion = page.evaluate(
        "matchMedia('(prefers-reduced-motion: reduce)').matches"
    )
    if reduced_motion:
        assert mask == "none", "Static reduced-motion columns should remain plainly scrollable"
    else:
        assert "linear-gradient" in mask, "Waterfall edges should use a vertical fade mask"
    expect(page.locator(".previewCard")).to_have_count(1)
    stage_angle = page.locator(".acceptedWaterfall").evaluate(
        """(stage) => {
          const matrix = new DOMMatrix(getComputedStyle(stage).transform);
          return Math.atan2(matrix.b, matrix.a) * 180 / Math.PI;
        }"""
    )
    if reduced_motion:
        assert abs(stage_angle) <= 0.1
    else:
        assert abs(stage_angle - 45) <= 0.1, f"Waterfall stage should rotate 45 degrees, got {stage_angle}"

    card_transform = page.locator(".acceptedCard").first.evaluate(
        "(card) => getComputedStyle(card).transform"
    )
    assert card_transform == "none", "Cards should inherit the stage angle without individual rotation"

    card_surfaces = page.locator(".acceptedWaterfallColumn").first.locator(".acceptedCard").evaluate_all(
        """(cards) => cards.slice(0, 3).map((card) => {
          const style = getComputedStyle(card);
          return { background: style.backgroundColor, border: style.borderTopColor };
        })"""
    )
    assert len({surface["background"] for surface in card_surfaces}) == 3
    assert all(surface["background"] == surface["border"] for surface in card_surfaces)

    spacing = page.locator(".acceptedWaterfallColumn").first.evaluate(
        """(column) => {
          const frames = [...column.querySelectorAll('.acceptedCardFrame')].slice(0, 3);
          const card = frames[0].querySelector('.acceptedCard');
          return {
            first: frames[1].offsetTop - frames[0].offsetTop,
            second: frames[2].offsetTop - frames[1].offsetTop,
            cardHeight: card.offsetHeight
          };
        }"""
    )
    assert abs(spacing["first"] - spacing["second"]) <= 1
    assert spacing["first"] - spacing["cardHeight"] >= 16

    adjacent_gap = page.locator(".acceptedWaterfall").evaluate(
        """(stage) => {
          const columns = stage.querySelectorAll('.acceptedWaterfallColumn');
          const first = columns[0].querySelector('.acceptedCard');
          const second = columns[1].querySelector('.acceptedCard');
          const firstLeft = columns[0].offsetLeft + first.offsetLeft;
          const secondLeft = columns[1].offsetLeft + second.offsetLeft;
          return secondLeft - (firstLeft + first.offsetWidth);
        }"""
    )
    assert adjacent_gap >= 0, f"Adjacent card columns should not overlap, got {adjacent_gap}px"

    tracks = page.locator(".acceptedWaterfallTrack")
    if reduced_motion:
        animation_name = tracks.first.evaluate("(node) => getComputedStyle(node).animationName")
        overflow_x = page.locator(".acceptedWaterfallViewport").evaluate(
            "(node) => getComputedStyle(node).overflowX"
        )
        assert animation_name == "none"
        assert overflow_x in {"auto", "scroll"}
    else:
        initial_positions = [
            tracks.nth(index).evaluate(
                "(node) => ({ x: node.getBoundingClientRect().x, y: node.getBoundingClientRect().y })"
            )
            for index in range(2)
        ]
        page.wait_for_timeout(700)
        later_positions = [
            tracks.nth(index).evaluate(
                "(node) => ({ x: node.getBoundingClientRect().x, y: node.getBoundingClientRect().y })"
            )
            for index in range(2)
        ]
        assert later_positions[0]["x"] > initial_positions[0]["x"]
        assert later_positions[0]["y"] < initial_positions[0]["y"]
        assert later_positions[1]["x"] < initial_positions[1]["x"]
        assert later_positions[1]["y"] > initial_positions[1]["y"]
    page.locator(".acceptedWaterfallViewport").hover()
    page.wait_for_timeout(120)
    like_buttons = page.locator(".acceptedLikeButton")
    clickable_indexes = like_buttons.evaluate_all(
        """(buttons) => buttons.flatMap((button, index) => {
          const rect = button.getBoundingClientRect();
          const x = rect.left + rect.width / 2;
          const y = rect.top + rect.height / 2;
          const hit = document.elementFromPoint(x, y);
          const count = button.textContent?.trim();
          return count?.endsWith('12') && x > 0 && x < innerWidth && y > 0 && y < innerHeight
            && hit && (hit === button || button.contains(hit))
            ? [index]
            : [];
        })"""
    )
    assert clickable_indexes, "At least one visible suggestion with 12 likes must be directly clickable"
    like_button = like_buttons.nth(clickable_indexes[0])
    expect(like_button).to_contain_text("12")
    like_button.click()
    expect(like_button).to_have_attribute("aria-pressed", "true")
    expect(like_button).to_contain_text("13")

    if lang == "zh-CN":
        page.route(
            "**/api/incentives/submissions",
            lambda route: route.fulfill(
                status=201,
                content_type="application/json",
                body='{"id":"21b0d5ea-8d2e-4d1e-91aa-4ad6ee01c001","status":"pending"}',
            ),
        )
        page.get_by_label("昵称").fill("测试岛民")
        page.get_by_label("邮箱").fill("islander@example.com")
        page.get_by_label("一句话标题").fill("希望加入更灵活的歌词行数")
        page.get_by_label("详细说明").fill("在窗口较窄时希望可以自动切换为单行歌词显示。")
        page.get_by_role("button", name="提交新功能提议").click()
        expect(page.get_by_role("heading", name="谢谢你的提交，如果被采纳我们将通过邮件联系你❤️")).to_be_visible()
        expect(page.get_by_role("dialog")).to_contain_text("测试岛民")
        expect(page.get_by_role("dialog")).to_contain_text("islander@example.com")
        expect(page.get_by_role("dialog")).to_contain_text("希望加入更灵活的歌词行数")
        page.screenshot(path=str(SCREENSHOTS / "submission-ticket.png"), full_page=False)
        with page.expect_download() as download_info:
            page.get_by_role("button", name="下载 PNG 存根").click()
        download = download_info.value
        assert download.suggested_filename.endswith(".png")
        download.save_as(str(SCREENSHOTS / "submission-ticket-download.png"))
        page.get_by_role("button", name="完成").click()
        cookie_names = [cookie["name"] for cookie in page.context.cookies()]
        assert "lyric_island_contributor" in cookie_names
        page.get_by_role("tab", name="Bug 提交").click()
        expect(page.get_by_label("昵称")).to_have_value("测试岛民")
        expect(page.get_by_label("邮箱")).to_have_value("islander@example.com")

    assert not errors, f"{path} console errors: {errors}"


def test_admin_shell(page: Page) -> None:
    response = page.goto(BASE_URL + "/admin/incentives", wait_until="networkidle")
    assert response is not None and response.ok
    expect(page.get_by_role("heading", name="审阅用户提交")).to_be_visible()
    expect(page.get_by_label("后台密码")).to_be_visible()
    expect(page.get_by_role("button", name="进入后台")).to_be_visible()


def test_waterfall_breakpoint(page: Page, expected_columns: int) -> None:
    response = page.goto(BASE_URL + "/incentives", wait_until="networkidle")
    assert response is not None and response.ok
    visible_columns = page.locator(".acceptedWaterfallColumn").evaluate_all(
        "(columns) => columns.filter((column) => getComputedStyle(column).display !== 'none').length"
    )
    assert visible_columns == expected_columns
    assert page.evaluate("document.documentElement.scrollWidth - innerWidth") <= 1


def test_admin_dashboard(page: Page) -> None:
    page.route(
        "**/api/incentives/admin/submissions",
        lambda route: route.fulfill(
            status=200,
            content_type="application/json",
            body=(
                '{"submissions":['
                '{"id":"s1","kind":"feature","nickname":"小林","email":"lin@example.com",'
                '"title":"自动切换歌词布局","body":"希望窗口变窄时自动切成单行。","attachments":[],"like_count":0,'
                '"status":"pending","reward_status":"not_eligible","reviewer_note":null,'
                '"created_at":"2026-07-14T05:00:00Z","updated_at":"2026-07-14T05:00:00Z"},'
                '{"id":"s2","kind":"bug","nickname":"Sea","email":"sea@example.com",'
                '"title":"切换播放器后歌词停住","body":"从 Spotify 切到酷狗时偶尔出现。","attachments":[],"like_count":0,'
                '"status":"reviewing","reward_status":"pending","reviewer_note":"等待复现",'
                '"created_at":"2026-07-14T04:00:00Z","updated_at":"2026-07-14T04:00:00Z"}'
                ']}'
            ),
        ),
    )
    page.route(
        "**/api/incentives/admin/previews",
        lambda route: route.fulfill(
            status=200,
            content_type="application/json",
            body=(
                '{"previews":[{"id":"p1","version":"v2.1 Preview",'
                '"title_zh":"更安静的桌面交互","title_en":"Quieter desktop interactions",'
                '"body_zh":"继续打磨避让和收起体验。","body_en":"More polish.",'
                '"highlights_zh":[],"highlights_en":[],"target_date":"2026-09-01",'
                '"status":"published","created_at":"2026-07-14T00:00:00Z",'
                '"updated_at":"2026-07-14T00:00:00Z","published_at":"2026-07-14T00:00:00Z"}]}'
            ),
        ),
    )
    response = page.goto(BASE_URL + "/admin/incentives", wait_until="networkidle")
    assert response is not None and response.ok
    expect(page.get_by_role("heading", name="审阅队列")).to_be_visible()
    expect(page.locator(".reviewCard")).to_have_count(2)
    page.get_by_role("button", name="版本预告").click()
    expect(page.get_by_role("heading", name="发布版本预告")).to_be_visible()
    expect(page.locator(".previewEditor")).to_be_visible()


def test_submission_validation(page: Page) -> None:
    response = page.request.post(
        BASE_URL + "/api/incentives/submissions",
        multipart={
            "kind": "feature",
            "nickname": "岛民",
            "email": "not-an-email",
            "title": "测试标题",
            "body": "这是足够长但邮箱无效的测试说明内容。",
        },
    )
    assert response.status == 400, "Public submissions must validate identity before storage"


def main() -> None:
    SCREENSHOTS.mkdir(parents=True, exist_ok=True)

    with sync_playwright() as playwright:
        browser = playwright.chromium.launch(headless=True, channel="chrome")

        for name, width, height in VIEWPORTS:
            context = browser.new_context(
                viewport={"width": width, "height": height},
                reduced_motion="reduce" if name == "mobile" else "no-preference",
            )
            page = context.new_page()
            assert_page_health(page, "/", "zh-CN")
            if name in {"desktop", "mobile"}:
                test_interactions(page, "zh")
            if name == "desktop":
                test_smooth_section_snap(page)
                test_navigation_and_orbit(page)
                test_selective_text_reveal(page)
            if name == "mobile":
                assert page.locator("html").get_attribute("data-snap-scroll") is None, (
                    "Reduced-motion mobile contexts must keep native scrolling"
                )
                test_selective_text_reveal(page, reduced_motion=True)
            page.goto(BASE_URL + "/", wait_until="networkidle")
            page.evaluate("window.scrollTo(0, 0)")
            settle_reveals_for_screenshot(page)
            page.screenshot(
                path=str(SCREENSHOTS / f"zh-{name}.png"),
                full_page=True,
            )
            context.close()

        for name, width, height in [("desktop", 1440, 900), ("mobile", 390, 844)]:
            context = browser.new_context(viewport={"width": width, "height": height})
            page = context.new_page()
            assert_page_health(page, "/en", "en")
            if name == "desktop":
                test_interactions(page, "en")
            page.goto(BASE_URL + "/en", wait_until="networkidle")
            page.evaluate("window.scrollTo(0, 0)")
            settle_reveals_for_screenshot(page)
            page.screenshot(
                path=str(SCREENSHOTS / f"en-{name}.png"),
                full_page=True,
            )
            context.close()

        for path, lang, filename in [
            ("/updates", "zh-CN", "zh-updates.png"),
            ("/en/updates", "en", "en-updates.png"),
        ]:
            context = browser.new_context(viewport={"width": 1024, "height": 900})
            page = context.new_page()
            assert_page_health(page, path, lang)
            expect(page.locator(".releaseSection")).to_have_count(6)
            expect(page.locator(".boundariesSection")).to_have_count(0)
            expect(page.locator(".updatesDownloads .button")).to_have_count(1)
            expect(page.locator(".updatesDownloads a[href*='apps.microsoft.com']")).to_have_count(1)
            expect(page.locator(".updatesDownloads a[href*='github.com']")).to_have_count(0)
            assert page.locator("[data-text-reveal]").count() == 4
            expect(page.locator(".releaseSection h2[data-text-reveal]")).to_have_count(0)
            page.goto(BASE_URL + path, wait_until="networkidle")
            page.evaluate("window.scrollTo(0, 0)")
            settle_reveals_for_screenshot(page)
            page.screenshot(
                path=str(SCREENSHOTS / filename),
                full_page=True,
            )
            context.close()

        for path, lang, filename, mobile in [
            ("/incentives", "zh-CN", "zh-incentives.png", False),
            ("/en/incentives", "en", "en-incentives-mobile.png", True),
        ]:
            context = browser.new_context(
                viewport={"width": 390, "height": 844} if mobile else {"width": 1440, "height": 900},
                reduced_motion="reduce" if mobile else "no-preference",
            )
            page = context.new_page()
            test_incentive_page(page, path, lang, mobile)
            page.goto(BASE_URL + path, wait_until="networkidle")
            page.evaluate("window.scrollTo(0, 0)")
            settle_reveals_for_screenshot(page)
            page.screenshot(path=str(SCREENSHOTS / filename), full_page=True)
            context.close()

        for width, expected_columns in [(1024, 4), (768, 3)]:
            context = browser.new_context(
                viewport={"width": width, "height": 900},
                reduced_motion="no-preference",
            )
            page = context.new_page()
            test_waterfall_breakpoint(page, expected_columns)
            context.close()

        context = browser.new_context(viewport={"width": 1280, "height": 900})
        page = context.new_page()
        test_admin_shell(page)
        test_submission_validation(page)
        page.screenshot(path=str(SCREENSHOTS / "admin-login.png"), full_page=True)
        context.close()

        context = browser.new_context(viewport={"width": 1440, "height": 1000})
        page = context.new_page()
        test_admin_dashboard(page)
        page.screenshot(path=str(SCREENSHOTS / "admin-dashboard.png"), full_page=True)
        context.close()

        browser.close()

    print(
        "PASS: responsive home pages, bilingual updates, Microsoft Store links, "
        "incentive forms, shared identity cookies, admin login, interactions, focus, images, and overflow"
    )


if __name__ == "__main__":
    main()
