import os
import json
from selenium import webdriver
from selenium.webdriver.chrome.service import Service
from webdriver_manager.chrome import ChromeDriverManager
from bs4 import BeautifulSoup
from urllib.parse import urlparse
from datetime import datetime

# Create the knowledge directory if it doesn't exist
if not os.path.exists("knowledge"):
    os.makedirs("knowledge")

def scrape_website(domain, subdomain=None):
    # Ensure the domain starts with http:// or https://
    if not domain.startswith('http://') and not domain.startswith('https://'):
        domain = 'http://' + domain

    # Set up Selenium WebDriver
    service = Service(ChromeDriverManager().install())
    options = webdriver.ChromeOptions()
    options.add_argument('--headless')
    driver = webdriver.Chrome(service=service, options=options)

    # Start scraping
    url = domain
    driver.get(url)
    page_source = driver.page_source
    soup = BeautifulSoup(page_source, 'html.parser')

    # Extract all links
    links = soup.find_all('a', href=True)
    resources = []
    for link in links:
        href = link['href']
        if subdomain and not href.startswith(subdomain):
            continue
        if href.startswith('/'):
            href = domain + href
        resources.append(href)

    # Generate a filename based on the domain and date
    parsed_url = urlparse(domain)
    base_domain = parsed_url.netloc.replace('.', '_')
    date_str = datetime.now().strftime("%d_%m_%Y")
    
    if parsed_url.path:
        path_part = parsed_url.path.strip('/').replace('/', '_')
        filename = f"{base_domain}_{path_part}_{date_str}.json"
    else:
        filename = f"{base_domain}_{date_str}.json"
    
    filepath = os.path.join("knowledge", filename)

    # Save to JSON
    with open(filepath, 'w') as f:
        json.dump(resources, f, indent=4)

    driver.quit()
    return filepath

# Example usage
if __name__ == "__main__":
    # This part is for testing the function standalone
    domain = "http://example.com"
    subdomain = None
    scrape_website(domain, subdomain)
