#include <stdio.h>
#include <string.h>
#include <zephyr/bluetooth/bluetooth.h>
#include <zephyr/bluetooth/hci.h>
#include <zephyr/device.h>
#include <zephyr/drivers/uart.h>
#include <zephyr/kernel.h>
#include <zephyr/usb/usb_device.h>

static const struct device *const uart_dev = DEVICE_DT_GET_ONE(zephyr_cdc_acm_uart);
static uint32_t sequence;

#define NAME_LEN 32
#define JSON_LEN 256

static void uart_write(const char *text)
{
	for (size_t i = 0; text[i] != '\0'; i++) {
		uart_poll_out(uart_dev, text[i]);
	}
}

static const char *addr_type_to_string(uint8_t type)
{
	switch (type) {
	case BT_ADDR_LE_PUBLIC:
		return "public";
	case BT_ADDR_LE_RANDOM:
		return "random";
	case BT_ADDR_LE_PUBLIC_ID:
		return "public_id";
	case BT_ADDR_LE_RANDOM_ID:
		return "random_id";
	default:
		return "unknown";
	}
}

static const char *adv_type_to_string(uint8_t type)
{
	switch (type) {
	case BT_GAP_ADV_TYPE_ADV_IND:
		return "connectable";
	case BT_GAP_ADV_TYPE_ADV_DIRECT_IND:
		return "directed";
	case BT_GAP_ADV_TYPE_ADV_SCAN_IND:
		return "scannable";
	case BT_GAP_ADV_TYPE_ADV_NONCONN_IND:
		return "non_connectable";
	case BT_GAP_ADV_TYPE_SCAN_RSP:
		return "scan_response";
	default:
		return "unknown";
	}
}

static void json_escape(char *target, size_t target_len, const char *source)
{
	size_t out = 0;

	for (size_t i = 0; source[i] != '\0' && out + 1 < target_len; i++) {
		char c = source[i];
		if ((c == '"' || c == '\\') && out + 2 < target_len) {
			target[out++] = '\\';
			target[out++] = c;
		} else if ((unsigned char)c >= 0x20) {
			target[out++] = c;
		}
	}

	target[out] = '\0';
}

static bool parse_name(struct bt_data *data, void *user_data)
{
	char *name = user_data;
	uint8_t len;

	if (data->type != BT_DATA_NAME_SHORTENED && data->type != BT_DATA_NAME_COMPLETE) {
		return true;
	}

	len = MIN(data->data_len, NAME_LEN - 1);
	memcpy(name, data->data, len);
	name[len] = '\0';
	return false;
}

static void device_found(const bt_addr_le_t *addr, int8_t rssi, uint8_t type, struct net_buf_simple *ad)
{
	char addr_text[BT_ADDR_STR_LEN];
	char name[NAME_LEN] = {0};
	char escaped_name[NAME_LEN * 2] = {0};
	char json[JSON_LEN];
	int written;

	bt_addr_to_str(&addr->a, addr_text, sizeof(addr_text));
	bt_data_parse(ad, parse_name, name);
	json_escape(escaped_name, sizeof(escaped_name), name);

	written = snprintf(json, sizeof(json),
			   "{\"v\":1,\"type\":\"adv\",\"seq\":%u,\"receiver\":\"A\","
			   "\"uptimeMs\":%lld,\"addr\":\"%s\",\"addrType\":\"%s\","
			   "\"rssi\":%d,\"name\":%s%s%s,\"advType\":\"%s\",\"dataLen\":%u}\n",
			   sequence++,
			   k_uptime_get(),
			   addr_text,
			   addr_type_to_string(addr->type),
			   rssi,
			   name[0] == '\0' ? "" : "\"",
			   name[0] == '\0' ? "null" : escaped_name,
			   name[0] == '\0' ? "" : "\"",
			   adv_type_to_string(type),
			   ad->len);

	if (written > 0 && written < sizeof(json)) {
		uart_write(json);
	}
}

static int start_scan(void)
{
	struct bt_le_scan_param scan_param = {
		.type = BT_LE_SCAN_TYPE_ACTIVE,
		.options = 0,
		.interval = BT_GAP_SCAN_FAST_INTERVAL,
		.window = BT_GAP_SCAN_FAST_WINDOW,
	};

	return bt_le_scan_start(&scan_param, device_found);
}

int main(void)
{
	int err;

	if (!device_is_ready(uart_dev)) {
		return 0;
	}

	err = usb_enable(NULL);
	if (err != 0) {
		return 0;
	}

	while (true) {
		uint32_t dtr = 0U;
		uart_line_ctrl_get(uart_dev, UART_LINE_CTRL_DTR, &dtr);
		if (dtr != 0U) {
			break;
		}
		k_sleep(K_MSEC(100));
	}

	uart_line_ctrl_set(uart_dev, UART_LINE_CTRL_DCD, 1);
	uart_line_ctrl_set(uart_dev, UART_LINE_CTRL_DSR, 1);
	k_msleep(250);

	err = bt_enable(NULL);
	if (err != 0) {
		uart_write("{\"v\":1,\"type\":\"error\",\"message\":\"bt_enable failed\"}\n");
		return 0;
	}

	err = start_scan();
	if (err != 0) {
		uart_write("{\"v\":1,\"type\":\"error\",\"message\":\"scan_start failed\"}\n");
		return 0;
	}

	uart_write("{\"v\":1,\"type\":\"status\",\"message\":\"EchoTrace Node scanning\"}\n");

	while (true) {
		k_sleep(K_SECONDS(1));
	}
}
